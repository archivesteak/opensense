using static OpenSense.Core.Hardware.Hid.UsbKeyboardProtocol;

namespace OpenSense.Core.Hardware.Hid;

/// <summary>
/// A USB keyboard Acer's software knows (<see cref="UsbKeyboardProtocol"/>): its settings (backlight auto-off, the
/// Windows key) through the interface with 9-byte feature reports, its lights through the FF02 interface. Used from
/// the firmware's thread (settings) and the lighting thread (lights), so calls take turns.
/// </summary>
public sealed class UsbKeyboardDevice : IDisposable
{
    private readonly object _gate = new();
    private readonly HidChannel _commands;
    private readonly HidChannel? _lighting;
    private readonly Action<TimeSpan> _sleep;

    private UsbKeyboardDevice(UsbKeyboardModel model, HidChannel commands, HidChannel? lighting, Action<TimeSpan> sleep)
    {
        Model = model;
        _commands = commands;
        _lighting = lighting;
        _sleep = sleep;
        Leds = model.Lighting ? UsbKeyboardLeds.For(model.VendorId, model.ProductId) : null;
    }

    public UsbKeyboardModel Model { get; }

    /// <summary>Which LED each key has; null when OpenSense has no table for the keyboard.</summary>
    public IReadOnlyDictionary<string, int>? Leds { get; }

    /// <summary>It has a lighting interface.</summary>
    public bool HasLighting => _lighting is not null;

    public HidDeviceInfo Info => _commands.Info;

    /// <summary>Opens the first keyboard Acer's software knows; null when there is none.</summary>
    /// <param name="sleep">Waits between reports (tests pass a no-op).</param>
    public static UsbKeyboardDevice? Open(IHidBus bus, Action<TimeSpan>? sleep = null)
    {
        foreach (var info in bus.Enumerate().Where(IsCommandInterface))
        {
            var model = Find(info.VendorId, info.ProductId)!;
            bool Same(HidDeviceInfo d) => d.VendorId == model.VendorId && d.ProductId == model.ProductId;
            if (HidChannel.Open(bus, info, d => Same(d) && IsCommandInterface(d)) is not { } commands)
                continue;
            HidChannel? lighting = null;
            if (model.Lighting)
            {
                lighting = commands.Info.UsagePage == LightingUsagePage
                    ? commands
                    : HidChannel.Open(bus, d => Same(d) && IsLightingInterface(d));
            }
            return new UsbKeyboardDevice(model, commands, lighting, sleep ?? Thread.Sleep);
        }
        return null;
    }

    /// <summary>Whether the backlight goes off after 30 s; null without an answer.</summary>
    public bool? ReadAutoOff() => Ask(UsbKeyboardProtocol.ReadAutoOff()) is { } answer ? DecodeAutoOff(answer) : null;

    public bool WriteAutoOff(bool on)
    {
        lock (_gate)
            return _commands.SetFeature(AutoOff(on));
    }

    /// <summary>Whether the Windows and Menu keys work; null without an answer.</summary>
    public bool? ReadWindowsKeyEnabled() => Ask(ReadWindowsKeys()) is { } answer ? !DecodeWindowsKeysLocked(answer) : null;

    public bool WriteWindowsKeyEnabled(bool enabled)
    {
        lock (_gate)
            return _commands.SetFeature(WindowsKeys(!enabled));
    }

    /// <summary>The lights' brightness (0..100); null without an answer.</summary>
    public int? ReadBrightness() => Ask(ReadState()) is { } answer ? DecodeBrightness(answer) : null;

    /// <summary>Sends a command sequence to the lighting interface, 15 ms apart.</summary>
    public bool Send(IReadOnlyList<byte[]> commands)
    {
        if (_lighting is null)
            return false;
        lock (_gate)
        {
            var ok = true;
            for (var i = 0; i < commands.Count; i++)
            {
                if (i > 0)
                    _sleep(CommandDelay);
                ok &= _lighting.SetFeature(commands[i]);
            }
            return ok;
        }
    }

    /// <summary>Per-key colours: start, 20 ms, the data 2 ms apart, 20 ms, show them.</summary>
    public bool Upload(IReadOnlyDictionary<int, RgbColor> leds, int brightness) =>
        Upload(UploadStart(false), UploadData(Model, leds), ShowUpload(brightness, false));

    /// <summary>The MagForce keys' colours (W, A, S, D).</summary>
    public bool UploadMagKeys(IReadOnlyList<RgbColor> keys, int brightness) =>
        Upload(UploadStart(true), [MagKeyData(keys)], ShowUpload(brightness, true));

    private bool Upload(byte[] start, IReadOnlyList<byte[]> data, byte[] show)
    {
        if (_lighting is null)
            return false;
        lock (_gate)
        {
            if (!_lighting.SetFeature(start))
                return false;
            _sleep(UploadDelay);
            var ok = true;
            for (var i = 0; i < data.Count; i++)
            {
                if (i > 0)
                    _sleep(DataDelay);
                ok &= _lighting.Write(data[i]);
            }
            _sleep(UploadDelay);
            return _lighting.SetFeature(show) && ok;
        }
    }

    /// <summary>Sends a request, then reads the answer 20 ms later.</summary>
    private byte[]? Ask(byte[] request)
    {
        lock (_gate)
        {
            if (!_commands.SetFeature(request))
                return null;
            _sleep(UploadDelay);
            var answer = new byte[CommandLength];
            return _commands.GetFeature(answer) ? answer : null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_lighting is not null && !ReferenceEquals(_lighting, _commands))
                _lighting.Dispose();
            _commands.Dispose();
        }
    }
}
