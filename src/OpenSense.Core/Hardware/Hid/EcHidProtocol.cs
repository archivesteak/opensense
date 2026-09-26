using System.Buffers.Binary;

namespace OpenSense.Core.Hardware.Hid;

/// <summary>What the embedded controller's HID interface is asked (the command's u16).</summary>
public enum EcHidCommand : ushort
{
    /// <summary>A system value, chosen by <see cref="EcHidStatus"/>.</summary>
    Status = 0x0000,

    /// <summary>The operating mode ("System Usage Mode").</summary>
    Mode = 0x0001,

    /// <summary>A GPU overclock profile.</summary>
    OverclockProfile = 0x0002,

    /// <summary>A device's settings; device 2 is the keyboard backlight.</summary>
    Device = 0x000A,
}

/// <summary>The values <see cref="EcHidCommand.Status"/> reads.</summary>
public enum EcHidStatus : byte
{
    Version = 0,

    /// <summary>1 while the battery can add to the adapter's power (as <c>GetGamingSysInfo(0x02)</c> byte 5).</summary>
    BatteryBoost = 2,

    /// <summary>Bit 0: the barrel adapter is connected (the other bits are the USB-C one's).</summary>
    Adapter = 3,

    /// <summary>How many GPU overclock profiles the firmware has.</summary>
    OverclockProfiles = 4,

    /// <summary>Which operating modes there are (<see cref="EcHidProtocol.Modes"/>).</summary>
    ModeCapability = 5,

    /// <summary>Non-zero while the controller allows only Quiet and Balanced.</summary>
    ModeLimit = 6,

    /// <summary>1 when the USB-C adapter is strong enough for every mode.</summary>
    UsbCAdapter = 7,
}

/// <summary>The interface's version, as its two bytes ("first.second").</summary>
public readonly record struct EcHidVersion(byte First, byte Second)
{
    /// <summary>From 0.6, status answers repeat the type and carry the value two bytes later.</summary>
    public bool EchoesStatusType => First > 0 || Second >= 6;

    /// <summary>From 0.6, the keyboard backlight's timeout is set here rather than through <c>APGeAction</c>.</summary>
    public bool HasBacklightTimeout => EchoesStatusType;

    public override string ToString() => $"{First}.{Second}";
}

/// <summary>An answer: <see cref="Data"/> is the report after its id (so the status is at 0 and the command at 2).</summary>
public sealed record EcHidReply(ushort Status, ushort Command, byte[] Data)
{
    public bool Done => Status == EcHidProtocol.StatusDone;

    /// <summary>A final answer: done, or not supported. Anything else is worth asking again.</summary>
    public bool Final => Status is EcHidProtocol.StatusDone or EcHidProtocol.StatusUnsupported;

    public byte Byte(int offset) => offset < Data.Length ? Data[offset] : (byte)0;

    public ushort Word(int offset) => offset + 1 < Data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(offset)) : (ushort)0;
}

/// <summary>
/// The embedded controller's HID interface on 2024+ Predators: feature report <c>A0</c> with a u16 header, a
/// command, a function or type byte and parameters, answered in the same report (docs/PROTOCOL.md,
/// "Embedded controller HID interface"). Pure encoders and decoders; offsets count from the byte after the report id.
/// </summary>
public static class EcHidProtocol
{
    public const ushort VendorId = 0x1025;
    public const ushort ProductId = 0x174B;
    public const ushort UsagePage = 0xFF05;
    public const ushort Usage = 0x0001;
    public const byte ReportId = 0xA0;

    /// <summary>The report id and 64 bytes.</summary>
    public const int ReportLength = 65;

    public const ushort RequestHeader = 0xA000;
    public const ushort StatusDone = 0xE000;
    public const ushort StatusUnsupported = 0xE001;

    public const byte Get = 2;
    public const byte Set = 1;

    public const byte KeyboardBacklight = 2;

    /// <summary>Profile fields: the core and memory offsets, in MHz.</summary>
    private const int CoreOffsetAt = 19;
    private const int MemoryOffsetAt = 21;

    /// <summary>Acer Quick Access's order: a mode's value counts down from the number of modes − 1 along it.</summary>
    private static readonly OperatingMode[] ModeOrder =
        [OperatingMode.Eco, OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo];

    public static bool Matches(HidDeviceInfo device) =>
        device.VendorId == VendorId && device.ProductId == ProductId && device.UsagePage == UsagePage && device.Usage == Usage;

    /// <summary>A whole report: the id, the header, <paramref name="command"/>, <paramref name="function"/>, then <paramref name="data"/>.</summary>
    public static byte[] Request(EcHidCommand command, byte function, params ReadOnlySpan<byte> data)
    {
        var report = new byte[ReportLength];
        report[0] = ReportId;
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(1), RequestHeader);
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(3), (ushort)command);
        report[5] = function;
        data.CopyTo(report.AsSpan(6));
        return report;
    }

    public static byte[] StatusRequest(EcHidStatus type) => Request(EcHidCommand.Status, (byte)type);

    public static byte[] ModeRequest() => Request(EcHidCommand.Mode, Get);

    public static byte[] SetModeRequest(byte value) => Request(EcHidCommand.Mode, Set, value);

    public static byte[] OverclockProfileRequest(byte index) => Request(EcHidCommand.OverclockProfile, Get, index);

    public static byte[] BacklightTimeoutRequest() => Request(EcHidCommand.Device, Get, KeyboardBacklight);

    public static byte[] SetBacklightTimeoutRequest(int brightness, int timeoutSeconds)
    {
        Span<byte> data = stackalloc byte[7];
        data[0] = KeyboardBacklight;
        BinaryPrimitives.WriteUInt16LittleEndian(data[1..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data[3..], (ushort)Math.Clamp(brightness, 0, 100));
        BinaryPrimitives.WriteUInt16LittleEndian(data[5..], (ushort)Math.Clamp(timeoutSeconds, 0, ushort.MaxValue));
        return Request(EcHidCommand.Device, Set, data);
    }

    /// <summary>The answer in <paramref name="report"/> (a whole report, id first); null when it isn't one.</summary>
    public static EcHidReply? Decode(ReadOnlySpan<byte> report)
    {
        if (report.Length < 6 || report[0] != ReportId)
            return null;
        var data = report[1..].ToArray();
        return new EcHidReply(BinaryPrimitives.ReadUInt16LittleEndian(data), BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2)), data);
    }

    /// <summary>
    /// Older firmware answers with the bytes at 4 and 5 and leaves <c>FFFF</c> at 6; newer repeats the type (0) at 4
    /// and answers at 6 and 7.
    /// </summary>
    public static EcHidVersion? DecodeVersion(EcHidReply reply)
    {
        if (!reply.Done || reply.Command != (ushort)EcHidCommand.Status)
            return null;
        if (reply.Word(6) == 0xFFFF)
            return new EcHidVersion(reply.Byte(4), reply.Byte(5));
        return reply.Byte(4) == (byte)EcHidStatus.Version ? new EcHidVersion(reply.Byte(6), reply.Byte(7)) : null;
    }

    /// <summary>A status value: u16 at 6 after the repeated type from 0.6, else u16 at 4.</summary>
    public static ushort? DecodeStatus(EcHidReply reply, EcHidStatus type, EcHidVersion version)
    {
        if (!reply.Done || reply.Command != (ushort)EcHidCommand.Status)
            return null;
        if (!version.EchoesStatusType)
            return reply.Word(4);
        return reply.Byte(4) == (byte)type ? reply.Word(6) : null;
    }

    /// <summary>The mode value (byte 4).</summary>
    public static byte? DecodeMode(EcHidReply reply) =>
        reply.Done && reply.Command == (ushort)EcHidCommand.Mode ? reply.Byte(4) : null;

    public static bool Accepted(EcHidReply reply, EcHidCommand command) => reply.Done && reply.Command == (ushort)command;

    /// <summary>A profile's core and memory offsets (MHz; the firmware has only positive ones).</summary>
    public static ClockOffsets? DecodeOverclockProfile(EcHidReply reply) =>
        reply.Done && reply.Command == (ushort)EcHidCommand.OverclockProfile
            ? new ClockOffsets(reply.Word(CoreOffsetAt), reply.Word(MemoryOffsetAt))
            : null;

    /// <summary>The keyboard backlight's brightness (%) and timeout (s; 0 = never off).</summary>
    public static (int Brightness, int TimeoutSeconds)? DecodeBacklightTimeout(EcHidReply reply) =>
        reply.Done && reply.Command == (ushort)EcHidCommand.Device && reply.Byte(5) == KeyboardBacklight
            ? (reply.Word(8), reply.Word(10))
            : null;

    /// <summary>
    /// The operating modes a mode capability stands for, in Acer Quick Access's order (Eco, Quiet, Balanced,
    /// Performance, Turbo); empty for values it doesn't know.
    /// </summary>
    public static IReadOnlyList<OperatingMode> Modes(ushort capability) => capability switch
    {
        1 or 2 => [OperatingMode.Balanced],
        3 => [OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance],
        4 => [OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo],
        5 => ModeOrder,
        _ => [],
    };

    /// <summary>The value <paramref name="mode"/> has among <paramref name="modes"/> (from <see cref="Modes"/>).</summary>
    public static byte? ModeValue(IReadOnlyList<OperatingMode> modes, OperatingMode mode)
    {
        var index = IndexOf(modes, mode);
        return index < 0 ? null : (byte)(modes.Count - 1 - index);
    }

    public static OperatingMode? ModeFromValue(IReadOnlyList<OperatingMode> modes, byte value) =>
        value < modes.Count ? modes[modes.Count - 1 - value] : null;

    /// <summary>The overclock profile <paramref name="mode"/> uses: its value, when there are that many profiles.</summary>
    public static byte? OverclockProfileFor(IReadOnlyList<OperatingMode> modes, OperatingMode mode, int profiles) =>
        ModeValue(modes, mode) is { } value && value < profiles ? value : null;

    public static bool BarrelAdapter(ushort adapter) => (adapter & 1) != 0;

    private static int IndexOf(IReadOnlyList<OperatingMode> modes, OperatingMode mode)
    {
        for (var i = 0; i < modes.Count; i++)
        {
            if (modes[i] == mode)
                return i;
        }
        return -1;
    }
}
