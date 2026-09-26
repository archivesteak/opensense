using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Control;

/// <summary>Runs work on the thread that owns the firmware (see <see cref="FanControlService"/>).</summary>
public interface IDeviceDispatcher
{
    Task<T> InvokeAsync<T>(Func<AcerDevice, T> action);
}

/// <summary>
/// Applies <see cref="KeyboardSettings"/> (backlight auto-off, Windows key, LCD overdrive) to the firmware: only what
/// changed, latest request wins, and everything again after resume. The backlight's colours are
/// <see cref="LightingService"/>'s.
/// </summary>
public sealed class KeyboardService
{
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(6);

    private readonly IDeviceDispatcher _dispatcher;
    private readonly KeyboardCapabilities _caps;
    private readonly UsbKeyboardDevice? _usb;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    private KeyboardSettings? _pending;
    private bool _pumping;
    private Task _pump = Task.CompletedTask;

    // What the firmware was last told (only touched on the dispatcher thread).
    private bool? _appliedAutoOff, _appliedWindowsKey, _appliedOverdrive;

    /// <param name="usb">A USB keyboard that keeps the Windows key or auto-off itself (<see cref="KeyboardCapabilities.UsbWindowsKey"/>).</param>
    public KeyboardService(IDeviceDispatcher dispatcher, KeyboardCapabilities capabilities, UsbKeyboardDevice? usb = null,
        TimeProvider? time = null)
    {
        _dispatcher = dispatcher;
        _caps = capabilities;
        _usb = usb;
        _time = time ?? TimeProvider.System;
    }

    public KeyboardSettings Current { get; private set; } = new();

    /// <summary>Raised (on a worker thread) when the firmware rejects a change.</summary>
    public event Action<ControlNotice>? Notice;

    public Task<KeyboardState> ReadStateAsync() => _dispatcher.InvokeAsync(d => KeyboardState.Read(d, _caps, _usb));

    /// <summary>Applies the members of <paramref name="settings"/> that differ from what was last applied.</summary>
    public Task ApplyAsync(KeyboardSettings settings)
    {
        lock (_gate)
        {
            Current = settings;
            _pending = settings;
            if (!_pumping)
            {
                _pumping = true;
                _pump = Task.Run(PumpAsync);
            }
            return _pump;
        }
    }

    /// <summary>Forgets what was applied and sends every configured setting again.</summary>
    public Task ReapplyAsync()
    {
        return _dispatcher.InvokeAsync(_ =>
        {
            _appliedAutoOff = _appliedWindowsKey = _appliedOverdrive = null;
            return true;
        }).ContinueWith(_ => ApplyAsync(Current), TaskScheduler.Default).Unwrap();
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            KeyboardSettings next;
            lock (_gate)
            {
                if (_pending is null)
                {
                    _pumping = false;
                    return;
                }
                next = _pending;
                _pending = null;
            }
            var failures = await _dispatcher.InvokeAsync(d => ApplyChanges(d, next)).ConfigureAwait(false);
            foreach (var failure in failures)
                Notice?.Invoke(new ControlNotice(failure));
        }
    }

    private List<NoticeKind> ApplyChanges(AcerDevice device, KeyboardSettings settings)
    {
        var failures = new List<NoticeKind>();

        if (_caps.BacklightAutoOff && settings.BacklightAutoOff is { } autoOff && autoOff != _appliedAutoOff)
        {
            bool ok;
            if (_caps.UsbBacklightTimeout)
            {
                ok = _usb?.WriteAutoOff(autoOff) == true;
            }
            else
            {
                // Only the timeout changes: the brightness goes back as it is.
                var brightness = device.GetBacklightTimeout(_caps)?.Brightness ?? 100;
                ok = device.SetBacklightTimeout(_caps, brightness, autoOff ? KeyboardProtocol.AutoOffSeconds : 0);
            }
            if (ok)
                _appliedAutoOff = autoOff;
            else
                failures.Add(NoticeKind.BacklightTimeoutRejected);
        }

        if (_caps.WindowsKey && settings.WindowsKey is { } winKey && winKey != _appliedWindowsKey)
        {
            if (_caps.UsbWindowsKey ? _usb?.WriteWindowsKeyEnabled(winKey) == true : device.SetWindowsKeyEnabled(winKey))
                _appliedWindowsKey = winKey;
            else
                failures.Add(NoticeKind.WindowsKeyRejected);
        }

        if (_caps.LcdOverdrive && settings.LcdOverdrive is { } overdrive && overdrive != _appliedOverdrive)
        {
            if (device.SetLcdOverdrive(overdrive))
                _appliedOverdrive = overdrive;
            else
                failures.Add(NoticeKind.LcdOverdriveRejected);
        }

        return failures;
    }

    /// <summary>The machine woke up: Acer's agent restores its own settings, so send ours again after it.</summary>
    /// <returns>Done once they are sent (the engine doesn't wait).</returns>
    public Task OnResume() => Task.Delay(ResumeDelay, _time).ContinueWith(_ => ReapplyAsync(), TaskScheduler.Default).Unwrap();
}
