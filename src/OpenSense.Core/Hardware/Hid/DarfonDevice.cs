using static OpenSense.Core.Hardware.Hid.DarfonProtocol;

namespace OpenSense.Core.Hardware.Hid;

/// <summary>A Darfon light device (<see cref="DarfonProtocol"/>). Not thread-safe: the engine uses it on the lighting thread.</summary>
public sealed class DarfonDevice : IDisposable
{
    private readonly HidChannel _channel;
    private readonly Action<TimeSpan> _sleep;

    private DarfonDevice(DarfonModel model, HidChannel channel, Action<TimeSpan> sleep)
    {
        Model = model;
        _channel = channel;
        _sleep = sleep;
    }

    public DarfonModel Model { get; }

    public HidDeviceInfo Info => _channel.Info;

    /// <summary>Opens every Darfon light device present, greeting each as Acer's software does.</summary>
    /// <param name="sleep">Waits between reports (tests pass a no-op).</param>
    public static IReadOnlyList<DarfonDevice> OpenAll(IHidBus bus, Action<TimeSpan>? sleep = null)
    {
        sleep ??= Thread.Sleep;
        List<DarfonDevice> devices = [];
        foreach (var info in bus.Enumerate().Where(Matches))
        {
            if (devices.Any(d => d.Model.ProductId == info.ProductId))
                continue;
            var product = info.ProductId;
            if (HidChannel.Open(bus, info, d => Matches(d) && d.ProductId == product) is not { } channel)
                continue;
            channel.Write(Hello());
            sleep(ShortDelay);
            devices.Add(new DarfonDevice(Find(info.VendorId, info.ProductId)!, channel, sleep));
        }
        return devices;
    }

    /// <summary>Sends reports <see cref="CommandDelay"/> apart.</summary>
    public bool Send(IReadOnlyList<byte[]> reports)
    {
        var ok = true;
        for (var i = 0; i < reports.Count; i++)
        {
            if (i > 0)
                _sleep(CommandDelay);
            ok &= _channel.SetFeature(reports[i]);
        }
        return ok;
    }

    /// <summary>The light bar's or ring's static colour: its steps 20 ms apart.</summary>
    public bool SendSteps(IReadOnlyList<IReadOnlyList<byte[]>> steps)
    {
        var ok = true;
        for (var i = 0; i < steps.Count; i++)
        {
            if (i > 0)
                _sleep(ShortDelay);
            foreach (var report in steps[i])
                ok &= _channel.SetFeature(report);
        }
        return ok;
    }

    public void Dispose() => _channel.Dispose();
}
