using System.Management;

namespace OpenSense.Core.Hardware;

/// <summary>What an <c>APGeEvent</c> is about: byte 0 of its detail.</summary>
public enum FirmwareEventKind : byte
{
    /// <summary>
    /// A hotkey: the value is the key's id, bytes 2-3 what the firmware adds about it (AN515-57: the touchpad key 0x83
    /// has bit 1 of byte 2 set when the touchpad is on).
    /// </summary>
    Hotkey = 1,

    /// <summary>A hotkey let go, probably (the value is the key's id, as for <see cref="Hotkey"/>).</summary>
    HotkeyReleased = 2,

    /// <summary>A brightness key (Fn+Left/Right on the AN515-57, after its <see cref="Hotkey"/> event); nothing more in it.</summary>
    Brightness = 4,

    /// <summary>Value 1: Dust Defender started or ended (see <see cref="FirmwareEvent.DustDefenderRunning"/>).</summary>
    Thermal = 6,

    /// <summary>The Mode (Turbo) key; the firmware leaves switching the mode to software.</summary>
    ModeKey = 7,

    /// <summary>The adapter was plugged in (value 1) or unplugged (0).</summary>
    AcAdapter = 8,

    /// <summary>
    /// The battery-boost flag changed. The value is meant to be the new flag, but the AN515-57 (V1.17) always sends 0,
    /// so the flag is read again (see <see cref="AcerProtocol.BatteryBoostValue"/>).
    /// </summary>
    BatteryBoost = 9,
    SimCard = 10,
    BatteryCalibration = 11,
    BatteryCharging = 16,
}

/// <summary>An event the firmware raised.</summary>
/// <param name="Detail">The whole <c>EventDetail</c>: byte 0 is the kind, byte 1 the value.</param>
public sealed record FirmwareEvent(FirmwareEventKind Kind, byte Value, byte[] Detail)
{
    /// <summary>Decodes an <c>APGeEvent</c>'s <c>EventDetail</c>; null when it is empty.</summary>
    public static FirmwareEvent? Decode(byte[]? detail) => detail is { Length: > 0 }
        ? new FirmwareEvent((FirmwareEventKind)detail[0], detail.Length > 1 ? detail[1] : (byte)0, [.. detail])
        : null;

    /// <summary>
    /// A thermal event with value 1 is about Dust Defender: byte 2 is 1 when a run starts and 0 when it ends.
    /// Null for any other event.
    /// </summary>
    public bool? DustDefenderRunning => Kind == FirmwareEventKind.Thermal && Value == 1 && Detail.Length > 2 ? Detail[2] == 1 : null;

    public override string ToString() => $"{Kind} {Convert.ToHexString(Detail)}";
}

/// <summary>Delivers the firmware's events.</summary>
public interface IFirmwareEvents : IDisposable
{
    /// <summary>Raised on a worker thread.</summary>
    event Action<FirmwareEvent>? Raised;

    /// <summary>Starts listening; false when the firmware has no event class (or it cannot be watched).</summary>
    bool Start();
}

/// <summary>
/// Watches the firmware's <c>APGeEvent</c> class in root\WMI (administrators only). If WMI ends the
/// subscription (e.g. its service restarts), it subscribes again.
/// </summary>
public sealed class WmiFirmwareEvents : IFirmwareEvents
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private ManagementEventWatcher? _watcher;
    private bool _disposed;

    public event Action<FirmwareEvent>? Raised;

    public bool Start()
    {
        lock (_gate)
        {
            if (_disposed)
                return false;
            var watcher = new ManagementEventWatcher(new ManagementScope(@"\\.\root\WMI"),
                new WqlEventQuery($"SELECT * FROM {AcerProtocol.EventClass}"));
            watcher.EventArrived += OnEventArrived;
            watcher.Stopped += OnStopped;
            try
            {
                watcher.Start();
            }
            catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException)
            {
                watcher.Dispose();
                return false;
            }
            _watcher = watcher;
            return true;
        }
    }

    private void OnEventArrived(object sender, EventArrivedEventArgs e)
    {
        if (FirmwareEvent.Decode(e.NewEvent["EventDetail"] as byte[]) is { } firmwareEvent)
            Raised?.Invoke(firmwareEvent);
    }

    private void OnStopped(object sender, StoppedEventArgs e)
    {
        ManagementEventWatcher stopped;
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(sender, _watcher))
                return;
            stopped = _watcher;
            _watcher = null;
        }
        _ = Task.Delay(RestartDelay).ContinueWith(_ =>
        {
            stopped.Dispose();
            Start();
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        ManagementEventWatcher? watcher;
        lock (_gate)
        {
            _disposed = true;
            watcher = _watcher;
            _watcher = null;
        }
        if (watcher is null)
            return;
        watcher.Stopped -= OnStopped;
        watcher.EventArrived -= OnEventArrived;
        try
        {
            watcher.Stop();
        }
        catch (ManagementException)
        {
            // Already gone.
        }
        watcher.Dispose();
    }
}
