using static OpenSense.Core.Hardware.Hid.Kyd100Protocol;

namespace OpenSense.Core.Hardware.Hid;

/// <summary>
/// The embedded controller's lighting HID interface (<see cref="Kyd100Protocol"/>). Every exchange is tried up to five
/// times, 20 ms apart, as Acer's software does. Not thread-safe: the engine uses it on the lighting thread.
/// </summary>
public sealed class Kyd100Device : IDisposable
{
    private const int Attempts = 5;

    /// <summary>How long the interface is given to answer, and the wait between tries and between LEDs.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(20);

    private readonly HidChannel _channel;
    private readonly Action<TimeSpan> _sleep;

    private Kyd100Device(HidChannel channel, Action<TimeSpan> sleep)
    {
        _channel = channel;
        _sleep = sleep;
    }

    public HidDeviceInfo Info => _channel.Info;

    /// <summary>Opens the interface; null when the laptop has none.</summary>
    /// <param name="sleep">Waits between reports (tests pass a no-op).</param>
    public static Kyd100Device? Open(IHidBus bus, Action<TimeSpan>? sleep = null) =>
        HidChannel.Open(bus, Matches) is { } channel ? new Kyd100Device(channel, sleep ?? Thread.Sleep) : null;

    /// <summary>The light ids as the interface lists them (report 0xA1); null when it doesn't answer.</summary>
    public IReadOnlyList<byte>? ReadLightIds()
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
                _sleep(Delay);
            var report = Buffer(Info, ListReport);
            if (_channel.GetFeature(report))
                return RawList(report);
        }
        return null;
    }

    /// <summary>Selects the light, then reads its info 20 ms later; null when no answer names it.</summary>
    public Kyd100LightInfo? ReadInfo(Kyd100Light id)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
                _sleep(Delay);
            if (!_channel.SetFeature(Select(Info, (byte)id)))
                continue;
            _sleep(Delay);
            var report = Buffer(Info, InfoReport);
            if (_channel.GetFeature(report) && DecodeInfo(Info, report, id) is { } info)
                return info;
        }
        return null;
    }

    public bool Update(Kyd100Light id, Kyd100Update update)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
                _sleep(Delay);
            if (_channel.SetFeature(Kyd100Protocol.Update(Info, id, update)))
                return true;
        }
        return false;
    }

    /// <summary>Static colours, LED by LED (mode static, that LED's bit), 20 ms apart.</summary>
    public bool SetStatic(Kyd100Light id, int brightness, IReadOnlyList<RgbColor> colors)
    {
        var ok = true;
        for (var led = 0; led < Math.Min(colors.Count, MaxZones); led++)
        {
            if (led > 0)
                _sleep(Delay);
            ok &= Update(id, new Kyd100Update(Kyd100Mode.Static, brightness, 0, 0, colors[led], (ushort)(1 << led)));
        }
        return ok;
    }

    public void Dispose() => _channel.Dispose();
}
