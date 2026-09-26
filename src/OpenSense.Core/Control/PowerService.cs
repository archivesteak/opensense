using OpenSense.Core.Hardware;
using OpenSense.Core.Settings;
using Windows.Win32;

namespace OpenSense.Core.Control;

/// <summary>
/// Battery and USB power. Applies <see cref="PowerSettings"/> where they differ from what the firmware has, at start
/// and again after resume (Acer's software writes its own choice then), and runs battery calibrations: the firmware
/// charges the battery full, runs it down on the adapter and charges it again, over several hours, and a sleep can end
/// it. Meanwhile Windows is kept awake and the lid and low-battery actions do nothing; everything is put
/// back when it ends, also after a crash (the calibration is recorded in <see cref="RuntimeState"/>).
/// </summary>
public sealed class PowerService : IDisposable
{
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan CalibrationPoll = TimeSpan.FromMinutes(1);
    private const uint DoNothing = 0;

    /// <summary>GUID_LIDSWITCH_ACTION (winnt.h), missing from the Win32 metadata.</summary>
    private static readonly Guid LidSwitchAction = new("5ca83367-6e45-459f-a27b-476b1d01c936");

    private static readonly PowerOverride[] CalibrationOverrides =
    [
        new(PInvoke.GUID_SYSTEM_BUTTON_SUBGROUP, LidSwitchAction, DoNothing),
        new(PInvoke.GUID_BATTERY_SUBGROUP, PInvoke.GUID_BATTERY_DISCHARGE_ACTION_0, DoNothing), // critical battery
        new(PInvoke.GUID_BATTERY_SUBGROUP, PInvoke.GUID_BATTERY_DISCHARGE_ACTION_1, DoNothing), // low battery
    ];

    private readonly IDeviceDispatcher _dispatcher;
    private readonly BatteryCapabilities _battery;
    private readonly bool _usbCharging;
    private readonly IPowerSource _power;
    private readonly ISystemPower _system;
    private readonly Action<CalibrationRecord?> _saveCalibration;
    private readonly Func<DateTime> _clock;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _calibrationGate = new(1, 1);
    private readonly Timer _poll;

    private PowerSettings? _pending;
    private bool _pumping;
    private Task _pump = Task.CompletedTask;
    private CalibrationRecord? _calibration;
    private bool _slept;
    private bool _disposed;

    // What the firmware was last told (only touched on the dispatcher thread).
    private bool? _appliedChargeLimit;
    private UsbChargingSettings? _appliedUsb;

    /// <param name="calibration">A calibration that was running when the engine last stopped.</param>
    /// <param name="saveCalibration">Stores the running calibration (null when none runs).</param>
    public PowerService(IDeviceDispatcher dispatcher, DeviceCapabilities capabilities, IPowerSource power, ISystemPower system,
        CalibrationRecord? calibration, Action<CalibrationRecord?> saveCalibration, Func<DateTime>? clock = null)
    {
        _dispatcher = dispatcher;
        _battery = capabilities.Battery;
        _usbCharging = capabilities.UsbCharging;
        _power = power;
        _system = system;
        _calibration = calibration;
        _saveCalibration = saveCalibration;
        _clock = clock ?? (() => DateTime.UtcNow);
        _poll = new Timer(_ => _ = CheckCalibrationAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public PowerSettings Current { get; private set; } = new();

    /// <summary>Raised (on a worker thread) when the firmware rejects a change or a calibration ends.</summary>
    public event Action<ControlNotice>? Notice;

    public CalibrationRecord? Calibration
    {
        get
        {
            lock (_gate)
                return _calibration;
        }
    }

    /// <summary>The battery for telemetry; null on machines without one.</summary>
    public BatteryTelemetry? Telemetry()
    {
        var status = _power.Read();
        var started = Calibration?.StartedUtc;
        return status.BatteryPercent is null && status.Charging is null && started is null
            ? null
            : new BatteryTelemetry(status.BatteryPercent, status.Charging, started);
    }

    /// <summary>Applies <paramref name="settings"/> and picks up a calibration that was running when the engine last stopped.</summary>
    public Task StartAsync(PowerSettings settings) =>
        Task.WhenAll(Calibration is not null ? ResumeCalibrationAsync() : Task.CompletedTask, ApplyAsync(settings));

    /// <summary>Applies the members of <paramref name="settings"/> that differ from what the firmware has.</summary>
    public Task ApplyAsync(PowerSettings settings)
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

    /// <summary>
    /// The machine is going to sleep. The poll stops until the resume, so that a timer overdue at wake-up can't
    /// report a calibration the sleep ended as finished.
    /// </summary>
    public void OnSuspend()
    {
        lock (_gate)
        {
            if (_disposed || _calibration is null)
                return;
            _slept = true;
            _poll.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>The machine woke up: check the calibration, and put back what Acer's software may have changed.</summary>
    public void OnResume() => _ = Task.Delay(ResumeDelay).ContinueWith(_ => ReapplyAsync(), TaskScheduler.Default).Unwrap();

    /// <summary>The adapter or the battery changed: look at the calibration now rather than at the next poll.</summary>
    public void OnFirmwareEvent(FirmwareEvent firmwareEvent)
    {
        if (firmwareEvent.Kind is FirmwareEventKind.AcAdapter or FirmwareEventKind.BatteryCalibration or FirmwareEventKind.BatteryCharging)
            _ = CheckCalibrationAsync();
    }

    public async Task<CalibrationResult> StartCalibrationAsync()
    {
        if (!_battery.Calibration)
            return CalibrationResult.Unsupported;
        await _calibrationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return CalibrationResult.Rejected;
            if (Calibration is not null)
                return CalibrationResult.AlreadyRunning;
            if (!_power.Read().OnAc)
                return CalibrationResult.NeedsAc;
            var (result, limitWasOn) = await _dispatcher.InvokeAsync(BeginCalibration).ConfigureAwait(false);
            if (result != CalibrationResult.Started)
                return result;
            var scheme = _system.Override(CalibrationOverrides);
            _system.HoldAwake();
            SetCalibration(new CalibrationRecord(_clock(), limitWasOn, scheme));
            return CalibrationResult.Started;
        }
        finally
        {
            _calibrationGate.Release();
        }
    }

    /// <summary>Stops a running calibration at the user's request.</summary>
    public async Task StopCalibrationAsync()
    {
        await _calibrationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Calibration is not { } record || _disposed)
                return;
            await _dispatcher.InvokeAsync(d => d.SetBatteryFunction(BatteryFunction.Calibration, false)).ConfigureAwait(false);
            await FinishAsync(record, null).ConfigureAwait(false);
        }
        finally
        {
            _calibrationGate.Release();
        }
    }

    /// <summary>
    /// Looks at a running calibration: ended by the firmware (finished, or interrupted if the machine slept since the
    /// last look), or to be stopped because the adapter is out.
    /// </summary>
    internal async Task CheckCalibrationAsync()
    {
        await _calibrationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Calibration is not { } record || _disposed)
                return;
            if (await _dispatcher.InvokeAsync(d => d.GetBatteryHealth()).ConfigureAwait(false) is not { } health)
                return; // asked again at the next poll
            bool slept;
            lock (_gate)
            {
                slept = _slept;
                _slept = false;
            }
            if (!health.Calibrating)
            {
                await FinishAsync(record, slept ? NoticeKind.CalibrationInterrupted : NoticeKind.CalibrationFinished).ConfigureAwait(false);
            }
            else if (!_power.Read().OnAc)
            {
                await _dispatcher.InvokeAsync(d => d.SetBatteryFunction(BatteryFunction.Calibration, false)).ConfigureAwait(false);
                await FinishAsync(record, NoticeKind.CalibrationStoppedUnplugged).ConfigureAwait(false);
            }
        }
        finally
        {
            _calibrationGate.Release();
        }
    }

    private async Task ResumeCalibrationAsync()
    {
        await _calibrationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Calibration is not { } record || _disposed)
                return;
            switch (await _dispatcher.InvokeAsync(d => d.GetBatteryHealth()).ConfigureAwait(false))
            {
                case { Calibrating: true }:
                    var scheme = record.PowerScheme ?? _system.Override(CalibrationOverrides);
                    _system.HoldAwake();
                    SetCalibration(record with { PowerScheme = scheme });
                    break;
                case not null:
                    // It ended while OpenSense was not running (a restart, a crash).
                    await FinishAsync(record, NoticeKind.CalibrationInterrupted).ConfigureAwait(false);
                    break;
                default:
                    SetCalibration(record); // asked again at the next poll
                    break;
            }
        }
        finally
        {
            _calibrationGate.Release();
        }
    }

    /// <summary>Acer's order: the 80 % limit off, then the calibration on. Returns whether the limit was on.</summary>
    private static (CalibrationResult Result, bool LimitWasOn) BeginCalibration(AcerDevice device)
    {
        if (device.GetBatteryHealth() is not { } health)
            return (CalibrationResult.Rejected, false);
        if (health.Calibrating)
            return (CalibrationResult.AlreadyRunning, health.HealthMode);
        if (health.HealthMode && !device.SetBatteryFunction(BatteryFunction.HealthMode, false))
            return (CalibrationResult.Rejected, true);
        if (device.SetBatteryFunction(BatteryFunction.Calibration, true))
            return (CalibrationResult.Started, health.HealthMode);
        if (health.HealthMode)
            device.SetBatteryFunction(BatteryFunction.HealthMode, true);
        return (CalibrationResult.Rejected, health.HealthMode);
    }

    private async Task FinishAsync(CalibrationRecord record, NoticeKind? notice)
    {
        lock (_gate)
        {
            _calibration = null;
            _slept = false;
            _poll.Change(Timeout.Infinite, Timeout.Infinite);
        }
        _system.ReleaseAwake();
        if (record.PowerScheme is { } scheme)
            _system.Restore(scheme);
        _saveCalibration(null);

        // The 80 % limit comes back as it was, or as the user set it meanwhile.
        if (_battery.ChargeLimit && (Current.ChargeLimit ?? record.ChargeLimitBefore))
        {
            var restored = await _dispatcher.InvokeAsync(d =>
            {
                var ok = d.SetBatteryFunction(BatteryFunction.HealthMode, true);
                if (ok)
                    _appliedChargeLimit = true;
                return ok;
            }).ConfigureAwait(false);
            if (!restored)
                Notice?.Invoke(new ControlNotice(NoticeKind.ChargeLimitRejected));
        }
        if (notice is { } kind)
            Notice?.Invoke(new ControlNotice(kind, Important: true));
    }

    private void SetCalibration(CalibrationRecord record)
    {
        lock (_gate)
        {
            _calibration = record;
            _poll.Change(CalibrationPoll, CalibrationPoll);
        }
        _saveCalibration(record);
    }

    private async Task ReapplyAsync()
    {
        await _dispatcher.InvokeAsync(_ =>
        {
            _appliedChargeLimit = null;
            _appliedUsb = null;
            return true;
        }).ConfigureAwait(false);
        await CheckCalibrationAsync().ConfigureAwait(false);
        lock (_gate)
        {
            if (!_disposed && _calibration is not null)
                _poll.Change(CalibrationPoll, CalibrationPoll);
        }
        await ApplyAsync(Current).ConfigureAwait(false);
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            PowerSettings next;
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

    private List<NoticeKind> ApplyChanges(AcerDevice device, PowerSettings settings)
    {
        var failures = new List<NoticeKind>();

        // A calibration turns the limit off until it ends.
        if (_battery.ChargeLimit && settings.ChargeLimit is { } limit && limit != _appliedChargeLimit && Calibration is null)
        {
            if (device.GetBatteryHealth()?.HealthMode == limit || device.SetBatteryFunction(BatteryFunction.HealthMode, limit))
                _appliedChargeLimit = limit;
            else
                failures.Add(NoticeKind.ChargeLimitRejected);
        }

        if (_usbCharging && settings.UsbCharging is { } usb && usb != _appliedUsb)
        {
            if (device.GetUsbCharging() == new UsbChargingState(usb.On, usb.Floor) || device.SetUsbCharging(usb.On, usb.Floor))
                _appliedUsb = usb;
            else
                failures.Add(NoticeKind.UsbChargingRejected);
        }

        return failures;
    }

    /// <summary>
    /// Stops watching. A running calibration goes on in the firmware: Windows' settings are put back now and changed
    /// again when the engine starts (the record keeps the calibration, without the settings).
    /// </summary>
    public void Dispose()
    {
        _calibrationGate.Wait();
        try
        {
            lock (_gate)
            {
                _disposed = true;
                _poll.Dispose();
            }
            if (Calibration is { } record)
            {
                _system.ReleaseAwake();
                if (record.PowerScheme is { } scheme)
                    _system.Restore(scheme);
                _saveCalibration(record with { PowerScheme = null });
            }
        }
        finally
        {
            _calibrationGate.Release();
        }
    }
}
