namespace OpenSense.Core.Hardware.Hid;

/// <summary>
/// An open HID interface that finds itself again: USB devices are enumerated anew after sleep, which leaves the old
/// handle dead. A failed call drops the handle; the next call opens the interface again (same kind of device, same
/// product), and a call that failed on a stale handle is tried once more on the new one. Not thread-safe.
/// </summary>
public sealed class HidChannel : IDisposable
{
    private readonly IHidBus _bus;
    private readonly Func<HidDeviceInfo, bool> _matches;
    private IHidDevice? _device;

    private HidChannel(IHidBus bus, Func<HidDeviceInfo, bool> matches, IHidDevice device)
    {
        _bus = bus;
        _matches = matches;
        _device = device;
        Info = device.Info;
    }

    public HidDeviceInfo Info { get; private set; }

    /// <summary>Opens the first interface <paramref name="matches"/> accepts; null when none opens.</summary>
    public static HidChannel? Open(IHidBus bus, Func<HidDeviceInfo, bool> matches)
    {
        foreach (var info in bus.Enumerate().Where(matches))
        {
            if (bus.Open(info) is { } device)
                return new HidChannel(bus, matches, device);
        }
        return null;
    }

    /// <summary>Opens <paramref name="info"/>, found again by <paramref name="matches"/> later; null when it doesn't open.</summary>
    public static HidChannel? Open(IHidBus bus, HidDeviceInfo info, Func<HidDeviceInfo, bool> matches) =>
        bus.Open(info) is { } device ? new HidChannel(bus, matches, device) : null;

    /// <summary>Reads a feature report into <paramref name="report"/> (<c>report[0]</c> is the report id).</summary>
    public bool GetFeature(byte[] report)
    {
        var id = report[0];
        return Call(device =>
        {
            report[0] = id;
            return device.GetFeature(report);
        });
    }

    public bool SetFeature(byte[] report) => Call(device => device.SetFeature(report));

    /// <summary>Sends an output report, padded with zeros to the interface's output length.</summary>
    public bool Write(byte[] report) => Call(device =>
    {
        if (device.Info.OutputLength <= report.Length)
            return device.Write(report);
        var padded = new byte[device.Info.OutputLength];
        report.CopyTo(padded, 0);
        return device.Write(padded);
    });

    private bool Call(Func<IHidDevice, bool> call)
    {
        var fresh = false;
        if (_device is null)
        {
            if (!Reopen())
                return false;
            fresh = true;
        }
        if (call(_device!))
            return true;
        Drop();
        // The handle may have been stale: once more on a new one (a device that refuses the call refuses it again).
        return !fresh && Reopen() && call(_device!);
    }

    private bool Reopen()
    {
        Drop();
        // The same interface where it is still listed, else another of its kind (a new path after re-enumeration).
        var candidates = _bus.Enumerate().Where(_matches)
            .OrderBy(d => d.Path == Info.Path ? 0 : 1)
            .ThenBy(d => d.ProductId == Info.ProductId ? 0 : 1);
        foreach (var info in candidates)
        {
            if (_bus.Open(info) is { } device)
            {
                _device = device;
                Info = info;
                return true;
            }
        }
        return false;
    }

    private void Drop()
    {
        _device?.Dispose();
        _device = null;
    }

    public void Dispose() => Drop();
}
