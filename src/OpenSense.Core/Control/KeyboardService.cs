using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Control;

/// <summary>Runs work on the thread that owns the firmware (see <see cref="FanControlService"/>).</summary>
public interface IDeviceDispatcher
{
    Task<T> InvokeAsync<T>(Func<AcerDevice, T> action);
}

/// <summary>
/// Applies <see cref="KeyboardSettings"/> (backlight auto-off, Windows key, Fn lock, LCD overdrive) to the firmware: only what
/// changed, latest request wins, and everything again after resume. The backlight's colours are
/// <see cref="LightingService"/>'s.
/// </summary>
public sealed class KeyboardService : IDisposable
{
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(6);

    private readonly IDeviceDispatcher _dispatcher;
    private readonly KeyboardCapabilities _caps;
    private readonly UsbKeyboardDevice? _usb;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly ServiceLifetime _lifetime = new();

    private KeyboardSettings? _pending;
    private bool _pumping;
    private Task _pump = Task.CompletedTask;

    // What the firmware was last told (only touched on the dispatcher thread).
    private bool? _appliedAutoOff, _appliedWindowsKey, _appliedOverdrive, _appliedFnLock;

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

    public Task<KeyboardState> ReadStateAsync() =>
        _lifetime.Run(_ => _dispatcher.InvokeAsync(d => KeyboardState.Read(d, _caps, _usb)));

    /// <summary>Applies the members of <paramref name="settings"/> that differ from what was last applied.</summary>
    public Task ApplyAsync(KeyboardSettings settings)
    {
        lock (_gate)
        {
            if (_lifetime.Stopped)
                return Task.CompletedTask;
            Current = settings;
            _pending = settings;
            if (!_pumping)
            {
                _pumping = true;
                _pump = _lifetime.Run(_ => Task.Run(PumpAsync, CancellationToken.None));
            }
            return _pump;
        }
    }

    /// <summary>Forgets what was applied and sends every configured setting again.</summary>
    public Task ReapplyAsync()
    {
        if (_lifetime.Stopped)
            return Task.CompletedTask;
        return _dispatcher.InvokeAsync(_ =>
        {
            _appliedAutoOff = _appliedWindowsKey = _appliedOverdrive = _appliedFnLock = null;
            return true;
        }).ContinueWith(_ => ApplyAsync(Current), TaskScheduler.Default).Unwrap();
    }

    private async Task PumpAsync()
    {
        try
        {
            await PumpLoopAsync().ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
                _pumping = false;
            throw;
        }
    }

    private async Task PumpLoopAsync()
    {
        while (true)
        {
            KeyboardSettings next;
            lock (_gate)
            {
                if (_pending is null || _lifetime.Stopped)
                {
                    _pumping = false;
                    return;
                }
                next = _pending;
                _pending = null;
            }
            try
            {
                var failures = await _dispatcher.InvokeAsync(d => _lifetime.Stopped ? [] : ApplyChanges(d, next)).ConfigureAwait(false);
                foreach (var failure in failures)
                    Notice?.Invoke(new ControlNotice(failure));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (!_lifetime.Stopped)
                    Notice?.Invoke(new ControlNotice(ex is AcerWmiAccessDeniedException ? NoticeKind.FirmwareAccessDenied : NoticeKind.ControlLoopError,
                        ex.Message));
            }
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

        if (_caps.FnLock && settings.FnLock is { } fnLock && fnLock != _appliedFnLock)
        {
            if (device.SetFnLock(fnLock))
                _appliedFnLock = fnLock;
            else
                failures.Add(NoticeKind.FnLockRejected);
        }

        return failures;
    }

    /// <summary>The machine woke up: Acer's agent restores its own settings, so send ours again after it.</summary>
    /// <returns>Done once they are sent (the engine doesn't wait).</returns>
    public Task OnResume() => _lifetime.Run(async token =>
    {
        await Task.Delay(ResumeDelay, _time, token).ConfigureAwait(false);
        if (!token.IsCancellationRequested)
            await ReapplyAsync().ConfigureAwait(false);
    });

    public Task StopAsync() => _lifetime.StopAsync();

    public void Dispose() => _lifetime.Dispose();
}
