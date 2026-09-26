using System.Buffers.Binary;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Tests;

/// <summary>
/// The embedded controller's HID interface as Acer's software describes it (docs/PROTOCOL.md): each SetFeature is
/// answered on the next GetFeature. By default a 2024 Predator's: version 0.7, all five modes (Balanced set), three
/// overclock profiles, the barrel adapter in.
/// </summary>
internal sealed class FakeEcHid : IHidDevice
{
    public static HidDeviceInfo Descriptor { get; } =
        new(@"\\?\hid#vid_1025&pid_174b&col02#simulated", EcHidProtocol.VendorId, EcHidProtocol.ProductId, 0x0100,
            EcHidProtocol.UsagePage, EcHidProtocol.Usage, 0, 0, EcHidProtocol.ReportLength);

    private byte[]? _answer;

    public HidDeviceInfo Info => Descriptor;

    public EcHidVersion Version { get; set; } = new(0, 7);

    /// <summary>The status values it answers; types left out are "not supported".</summary>
    public Dictionary<EcHidStatus, ushort> Status { get; } = new()
    {
        [EcHidStatus.ModeCapability] = 5,
        [EcHidStatus.BatteryBoost] = 1,
        [EcHidStatus.Adapter] = 1,
        [EcHidStatus.ModeLimit] = 0,
        [EcHidStatus.UsbCAdapter] = 1,
        [EcHidStatus.OverclockProfiles] = 3,
    };

    /// <summary>The mode value (2 = Balanced among five modes).</summary>
    public byte Mode { get; set; } = 2;

    public List<byte> ModesSet { get; } = [];

    /// <summary>Profiles 0 (Turbo), 1 (Performance) and 2 (Balanced) with five modes.</summary>
    public List<ClockOffsets> Profiles { get; } = [new(150, 200), new(100, 100), new(50, 0)];

    public (int Brightness, int TimeoutSeconds) Backlight { get; set; } = (100, 30);

    /// <summary>Answers this many requests with status 0 (neither done nor refused) first.</summary>
    public int Busy { get; set; }

    /// <summary>Fails this many SetFeature calls first, as a handle gone stale does.</summary>
    public int Broken { get; set; }

    public int Requests { get; private set; }

    public bool SetFeature(ReadOnlySpan<byte> report)
    {
        if (Broken > 0)
        {
            Broken--;
            return false;
        }
        Requests++;
        _answer = Answer(report);
        return true;
    }

    public bool GetFeature(Span<byte> report)
    {
        if (_answer is null)
            return false;
        _answer.AsSpan(0, Math.Min(_answer.Length, report.Length)).CopyTo(report);
        _answer = null;
        return true;
    }

    public bool Write(ReadOnlySpan<byte> report) => false;

    public void Dispose() { }

    /// <summary>The answer to <paramref name="request"/>; offsets past the report id as in docs/PROTOCOL.md.</summary>
    private byte[] Answer(ReadOnlySpan<byte> request)
    {
        var answer = new byte[EcHidProtocol.ReportLength];
        answer[0] = EcHidProtocol.ReportId;
        var data = answer.AsSpan(1);
        var command = (EcHidCommand)BinaryPrimitives.ReadUInt16LittleEndian(request[3..]);
        var function = request[5];
        BinaryPrimitives.WriteUInt16LittleEndian(data[2..], (ushort)command);
        ushort status = EcHidProtocol.StatusDone;
        if (Busy > 0)
        {
            Busy--;
            status = 0;
        }
        else
        {
            switch (command)
            {
                case EcHidCommand.Status when (EcHidStatus)function == EcHidStatus.Version:
                    if (Version.EchoesStatusType)
                    {
                        data[6] = Version.First;
                        data[7] = Version.Second;
                    }
                    else
                    {
                        data[4] = Version.First;
                        data[5] = Version.Second;
                        data[6] = data[7] = 0xFF;
                    }
                    break;
                case EcHidCommand.Status when Status.TryGetValue((EcHidStatus)function, out var value):
                    if (Version.EchoesStatusType)
                    {
                        data[4] = function;
                        BinaryPrimitives.WriteUInt16LittleEndian(data[6..], value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(data[4..], value);
                    }
                    break;
                case EcHidCommand.Mode when function == EcHidProtocol.Get:
                    data[4] = Mode;
                    break;
                case EcHidCommand.Mode when function == EcHidProtocol.Set:
                    Mode = request[6];
                    ModesSet.Add(Mode);
                    break;
                case EcHidCommand.OverclockProfile when request[6] < Profiles.Count:
                    BinaryPrimitives.WriteUInt16LittleEndian(data[19..], (ushort)Profiles[request[6]].CoreMhz);
                    BinaryPrimitives.WriteUInt16LittleEndian(data[21..], (ushort)Profiles[request[6]].MemoryMhz);
                    break;
                case EcHidCommand.Device when request[6] == EcHidProtocol.KeyboardBacklight && Version.HasBacklightTimeout:
                    data[5] = EcHidProtocol.KeyboardBacklight;
                    if (function == EcHidProtocol.Set)
                        Backlight = (BinaryPrimitives.ReadUInt16LittleEndian(request[9..]), BinaryPrimitives.ReadUInt16LittleEndian(request[11..]));
                    BinaryPrimitives.WriteUInt16LittleEndian(data[8..], (ushort)Backlight.Brightness);
                    BinaryPrimitives.WriteUInt16LittleEndian(data[10..], (ushort)Backlight.TimeoutSeconds);
                    break;
                default:
                    status = EcHidProtocol.StatusUnsupported;
                    break;
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data, status);
        return answer;
    }
}

/// <summary>HID devices the test puts on the machine.</summary>
internal sealed class FakeHidBus : IHidBus
{
    public List<IHidDevice> Devices { get; } = [];

    public IReadOnlyList<HidDeviceInfo> Enumerate() => [.. Devices.Select(d => d.Info)];

    public IHidDevice? Open(HidDeviceInfo device) => Devices.FirstOrDefault(d => d.Info == device);

    public event Action<string>? Changed;

    /// <summary>Takes a device away (or puts one back) and says so, as Windows does.</summary>
    public void Remove(IHidDevice device)
    {
        Devices.Remove(device);
        Changed?.Invoke(device.Info.Path);
    }

    public void Add(IHidDevice device)
    {
        Devices.Add(device);
        Changed?.Invoke(device.Info.Path);
    }
}

/// <summary>
/// The embedded controller's lighting HID interface (docs/PROTOCOL.md): lists <see cref="Lights"/>, answers each
/// one's info after it is selected, and records the updates it is sent.
/// </summary>
internal sealed class FakeKyd100 : IHidDevice
{
    public const int ReportLength = 32;

    public static HidDeviceInfo Descriptor { get; } =
        new(@"\\?\hid#vid_1025&pid_1765&col05#simulated", 0x1025, 0x1765, 0x0100, Kyd100Protocol.UsagePage, 1, 0, 0, ReportLength)
        {
            FeatureValues =
            [
                Field(Kyd100Protocol.InfoReport, 0x34, 8), Field(Kyd100Protocol.InfoReport, 0x32, 8), Field(Kyd100Protocol.InfoReport, 0x35, 8),
                Field(Kyd100Protocol.InfoReport, 0x33, 8), Field(Kyd100Protocol.InfoReport, 0x31, 32),
                Field(Kyd100Protocol.UpdateReport, 0x41, 8), Field(Kyd100Protocol.UpdateReport, 0x43, 8), Field(Kyd100Protocol.UpdateReport, 0x42, 8),
                Field(Kyd100Protocol.UpdateReport, 0x44, 8), Field(Kyd100Protocol.UpdateReport, 0x45, 8), Field(Kyd100Protocol.UpdateReport, 0x46, 8),
                Field(Kyd100Protocol.UpdateReport, 0x47, 8), Field(Kyd100Protocol.UpdateReport, 0x48, 16),
            ],
        };

    private static HidValueField Field(byte report, ushort usage, int bits) => new(report, Kyd100Protocol.UsagePage, usage, bits, 1);

    private byte _selected;

    public HidDeviceInfo Info => Descriptor;

    /// <summary>Its lights: id → (LEDs, attribute bits).</summary>
    public Dictionary<byte, (int Leds, uint Attributes)> Lights { get; } = [];

    /// <summary>Update reports as sent (light id first, then the values).</summary>
    public List<byte[]> Updates { get; } = [];

    public bool GetFeature(Span<byte> report)
    {
        report[1..].Clear();
        switch (report[0])
        {
            case Kyd100Protocol.ListReport:
                report[1] = (byte)Lights.Count;
                var i = 2;
                foreach (var id in Lights.Keys)
                    report[i++] = id;
                return true;
            case Kyd100Protocol.InfoReport when Lights.TryGetValue(_selected, out var light):
                report[1] = _selected;
                report[4] = (byte)light.Leds;
                BinaryPrimitives.WriteUInt32LittleEndian(report[6..], light.Attributes);
                return true;
            default:
                return false;
        }
    }

    public bool SetFeature(ReadOnlySpan<byte> report)
    {
        if (report.Length != ReportLength)
            return false;
        switch (report[0])
        {
            case Kyd100Protocol.SelectReport:
                _selected = report[1];
                return true;
            case Kyd100Protocol.UpdateReport:
                Updates.Add(report[1..11].ToArray());
                return true;
            default:
                return false;
        }
    }

    public bool Write(ReadOnlySpan<byte> report) => false;

    public void Dispose() { }
}

/// <summary>
/// A per-key USB keyboard (docs/PROTOCOL.md): records its commands (checking their checksums) and per-key data,
/// answers the Windows-key, auto-off and state reads.
/// </summary>
internal sealed class FakeUsbKeyboard(ushort vendorId, ushort productId) : IHidDevice
{
    private byte _asked;

    public HidDeviceInfo Info { get; } = new($@"\\?\hid#vid_{vendorId:x4}&pid_{productId:x4}&mi_03#simulated", vendorId, productId, 0x0100,
        UsbKeyboardProtocol.LightingUsagePage, 1, 0, 65, UsbKeyboardProtocol.CommandLength);

    /// <summary>The commands' eight bytes, in order.</summary>
    public List<byte[]> Commands { get; } = [];

    /// <summary>The output reports' 64 bytes, in order.</summary>
    public List<byte[]> Data { get; } = [];

    public bool WindowsKeyLocked { get; set; }

    public bool AutoOff { get; set; }

    public int Brightness { get; set; } = 50;

    public int BadChecksums { get; private set; }

    public bool GetFeature(Span<byte> report)
    {
        if (report.Length != UsbKeyboardProtocol.CommandLength)
            return false;
        report[1..].Clear();
        report[1] = _asked;
        switch (_asked)
        {
            case 0x83:
                report[2] = WindowsKeyLocked ? (byte)1 : (byte)0;
                return true;
            case 0xB0:
                report[3] = AutoOff ? (byte)1 : (byte)0;
                return true;
            case 0x88:
                report[5] = (byte)Brightness;
                return true;
            default:
                return false;
        }
    }

    public bool SetFeature(ReadOnlySpan<byte> report)
    {
        if (report.Length != UsbKeyboardProtocol.CommandLength || report[0] != 0)
            return false;
        var command = report[1..].ToArray();
        var sum = 0;
        for (var i = 0; i < 7; i++)
            sum += command[i];
        if ((byte)(0xFF - sum) != command[7])
            BadChecksums++;
        Commands.Add(command);
        _asked = command[0];
        if (command[0] == 0x03)
            WindowsKeyLocked = command[1] == 3;
        if (command[0] == 0x30)
            AutoOff = command[2] == 1;
        return true;
    }

    public bool Write(ReadOnlySpan<byte> report)
    {
        if (report.Length != Info.OutputLength || report[0] != 0)
            return false;
        Data.Add(report[1..].ToArray());
        return true;
    }

    public void Dispose() { }
}

/// <summary>Records the reports sent to it and answers feature reads with <see cref="Answer"/>.</summary>
internal sealed class FakeHidDevice(HidDeviceInfo info) : IHidDevice
{
    public HidDeviceInfo Info { get; } = info;

    public List<byte[]> Features { get; } = [];

    public List<byte[]> Writes { get; } = [];

    /// <summary>The feature report to answer a read with (by report id); null fails the read.</summary>
    public Func<byte, byte[]?>? Answer { get; set; }

    public bool GetFeature(Span<byte> report)
    {
        if (Answer?.Invoke(report[0]) is not { } answer)
            return false;
        answer.AsSpan(0, Math.Min(answer.Length, report.Length)).CopyTo(report);
        return true;
    }

    public bool SetFeature(ReadOnlySpan<byte> report)
    {
        Features.Add(report.ToArray());
        return true;
    }

    public bool Write(ReadOnlySpan<byte> report)
    {
        Writes.Add(report.ToArray());
        return true;
    }

    public void Dispose() { }
}
