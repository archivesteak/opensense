using System.Collections.Concurrent;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Monitoring;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Control;

/// <summary>
/// Owns the firmware: polls sensors, applies the user's <see cref="ControlProfile"/>, runs fan curves
/// and enforces safety rules. All WMI traffic happens on its single worker thread.
/// Fan speeds are boosts on top of the firmware's Auto (see <see cref="AcerProtocol.FanSpeedInput"/>), sent in the
/// firmware's steps of <see cref="AcerProtocol.FanSpeedStep"/> %.
/// </summary>
public sealed class FanControlService : IDeviceDispatcher, IDisposable
{
    private static readonly TimeSpan ReassertInterval = TimeSpan.FromSeconds(30);

    /// <summary>Curves are re-evaluated this often, on the average temperature since the last time.</summary>
    internal static readonly TimeSpan CurveInterval = TimeSpan.FromSeconds(3);
    private const int FailsafeAfterMisses = 3;

    /// <summary>How often a Dust Defender run is checked on, and how long one is waited for at most.</summary>
    private static readonly TimeSpan DustDefenderPoll = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DustDefenderLongest = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How often the battery-boost flag (and the embedded controller's adapter state) is read on AC, besides when the
    /// adapter comes or goes and on their events.
    /// </summary>
    private static readonly TimeSpan BatteryBoostInterval = TimeSpan.FromSeconds(30);

    private readonly AcerDevice _device;
    private readonly IOperatingModeChannel _modes;
    private readonly IReadOnlyList<FanChannel> _controllable;
    private readonly ILoadMonitor _load;
    private readonly IPowerSource _power;
    private readonly DirectSensors _direct;
    private readonly Func<DateTime> _clock;
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly ConcurrentQueue<Action> _work = new();
    private readonly Dictionary<FanId, CurveFollower> _followers = new();
    private readonly List<DateTime> _reapplyAt = [];
    private readonly object _gate = new();

    private ControlProfile _profile;
    private volatile bool _stopping;

    // Last sensor sample.
    private DateTime _lastSample = DateTime.MinValue;
    private double? _cpuTemp, _gpuTemp, _systemTemp, _cpuLoad, _gpuLoad;
    private bool _gpuAsleep;
    private bool? _gpuOn;
    private TemperatureOrigin _cpuOrigin, _gpuOrigin;
    private ThermalLimits _limits = ThermalLimits.Default;
    private readonly Dictionary<FanId, int?> _rpm = new();

    // Curve steps: each fan's temperature summed since the last step.
    private readonly Dictionary<FanId, (double Sum, int Count)> _curveTemps = new();
    private DateTime _lastCurveStep = DateTime.MinValue;
    private ControlProfile? _curveProfile;
    private FanControlMode? _curveMode;

    // What the firmware was last told.
    private Dictionary<FanId, FanBehavior>? _appliedBehavior;
    private readonly Dictionary<FanId, int> _appliedPercent = new();
    private OperatingMode? _appliedMode;
    private bool? _appliedCoolBoost;
    private FanTable? _appliedFanTable;
    private DateTime _lastAssert = DateTime.MinValue;
    private bool _touchedFans;
    private bool _defaultModeChecked;

    // Safety state.
    private bool _failsafe;
    private int _cpuMisses;

    // Dust Defender: while the firmware runs the fans backwards, OpenSense leaves them alone.
    private DateTime? _dustSince;
    private DateTime _dustNextCheck;
    private bool _dustChecked;

    // What the power supply allows.
    private bool? _batteryBoost;
    private bool? _weakAdapter;
    private DateTime _nextBoostRead = DateTime.MinValue;
    private bool? _lastOnAc;
    private PowerLimit _limit;
    private PowerLimit? _lastLimit;

    private readonly GpuClockController? _gpuClocks;

    /// <param name="knownGpuClocks">What the GPU's driver said about clock offsets last time, for this GPU.</param>
    /// <param name="modes">
    /// Where the operating mode is set: unless given, the embedded controller's HID interface where it sets the modes,
    /// else the gaming WMI interface.
    /// </param>
    public FanControlService(AcerDevice device, DeviceCapabilities capabilities, ILoadMonitor load, IPowerSource power,
        ControlProfile initialProfile, DirectSensors? direct = null, Func<DateTime>? clock = null, GpuClockRecord? knownGpuClocks = null,
        IOperatingModeChannel? modes = null)
    {
        _device = device;
        _modes = modes ?? (capabilities.EcHid is { Modes.Count: > 0 } ecHid && device.EcHid is { } hid
            ? new EcHidOperatingModeChannel(device, hid, ecHid.Modes)
            : new WmiOperatingModeChannel(device));
        Capabilities = capabilities;
        _load = load;
        _power = power;
        _direct = direct ?? DirectSensors.None;
        _profile = initialProfile;
        _clock = clock ?? (() => DateTime.UtcNow);
        _controllable = capabilities.ControllableFans;
        foreach (var fan in _controllable)
            _followers[fan.Id] = new CurveFollower();
        if (_direct.GpuClocks is { } gpuClocks)
        {
            _gpuClocks = new GpuClockController(gpuClocks, knownGpuClocks);
            _gpuClocks.LimitsChanged += limits => GpuClockLimitsChanged?.Invoke(limits);
            _gpuClocks.Refused += () => Notice?.Invoke(new ControlNotice(NoticeKind.GpuClocksRejected));
        }
        _thread = new Thread(Run) { IsBackground = true, Name = "OpenSense control loop" };
    }

    public DeviceCapabilities Capabilities { get; }

    /// <summary>How often sensors are read: fixed at a second (curves act every <see cref="CurveInterval"/>); tests shorten it.</summary>
    public TimeSpan Interval { get; internal set; } = TimeSpan.FromSeconds(1);

    public Telemetry? Latest { get; private set; }

    /// <summary>Raised on the control thread after every sample.</summary>
    public event Action<Telemetry>? TelemetryUpdated;

    /// <summary>Raised on the control thread for things the user should know about.</summary>
    public event Action<ControlNotice>? Notice;

    /// <summary>Raised on the control thread when the GPU's driver reports new clock offset limits (null: it has none).</summary>
    public event Action<GpuClockLimits?>? GpuClockLimitsChanged;

    public void Start() => _thread.Start();

    public ControlProfile Profile
    {
        get
        {
            lock (_gate)
                return _profile;
        }
    }

    public void Update(ControlProfile profile)
    {
        lock (_gate)
            _profile = profile;
        _wake.Set();
    }

    /// <summary>Runs <paramref name="action"/> on the control thread (serialised with all other firmware calls).</summary>
    public Task<T> InvokeAsync<T>(Func<AcerDevice, T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(() =>
        {
            try
            {
                tcs.SetResult(action(_device));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        _wake.Set();
        return tcs.Task;
    }

    /// <summary>Re-sends the full state to the firmware after each delay (e.g. after resume, when Acer's agent may overwrite it).</summary>
    public void ReapplyAfter(params TimeSpan[] delays)
    {
        var now = _clock();
        lock (_reapplyAt)
            _reapplyAt.AddRange(delays.Select(d => now + d));
        _wake.Set();
    }

    /// <summary>The machine woke up: Acer's agent may restore its own settings, so send ours again after it.</summary>
    public void OnResume()
    {
        _work.Enqueue(() => _nextBoostRead = DateTime.MinValue); // the battery may have run down meanwhile
        ReapplyAfter(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(12));
    }

    /// <summary>
    /// AC power was connected or removed (or, as its event says, another adapter plugged in): re-evaluate the operating
    /// mode right away.
    /// </summary>
    public void OnPowerSourceChanged()
    {
        _work.Enqueue(() => _nextBoostRead = DateTime.MinValue);
        _wake.Set();
    }

    /// <summary>
    /// The firmware's battery-boost flag changed (its event 9): read it again. The event's own byte doesn't count: the
    /// AN515-57 V1.17 always sends 0 there, and where the embedded controller's HID interface keeps the flag, Acer's
    /// software reads it from there.
    /// </summary>
    public void OnBatteryBoostEvent()
    {
        if (!Capabilities.BatteryBoostFlag)
            return;
        _work.Enqueue(() => _nextBoostRead = DateTime.MinValue);
        _wake.Set();
    }

    /// <summary>
    /// Works out where the Mode key goes from the mode in force (on the control thread, which knows the power supply);
    /// the engine stores the choice in the profile.
    /// </summary>
    public Task<ModeKeyPress> PressModeKeyAsync(ControlProfile profile) => InvokeAsync(_ =>
    {
        var current = _appliedMode ?? _modes.Read();
        return new ModeKeyPress(current,
            OperatingModePolicy.NextForKey(profile, current, Capabilities.OperatingModes, _limit, Capabilities.ModeRules), _limit);
    });

    /// <summary>Asks the firmware to run the fans backwards for a moment to blow the dust out (Dust Defender).</summary>
    public Task<DustDefenderStart> StartDustDefenderAsync() => InvokeAsync(device =>
    {
        if (!Capabilities.DustDefender)
            return DustDefenderStart.Failed;
        if (_dustSince is not null)
            return DustDefenderStart.Running;
        var result = device.StartDustDefender();
        if (result == DustDefenderStart.Started)
            DustDefenderStarted(_clock());
        return result;
    });

    /// <summary>The firmware reported a Dust Defender run starting or ending (it also starts some by itself).</summary>
    public void OnDustDefenderEvent(bool running)
    {
        if (!Capabilities.DustDefender)
            return;
        _work.Enqueue(() =>
        {
            if (running)
                DustDefenderStarted(_clock());
            else
                DustDefenderEnded();
        });
        _wake.Set();
    }

    private void Run()
    {
        while (!_stopping)
        {
            try
            {
                Tick();
            }
            catch (AcerWmiAccessDeniedException)
            {
                Notice?.Invoke(new ControlNotice(NoticeKind.FirmwareAccessDenied));
            }
            catch (Exception ex)
            {
                Notice?.Invoke(new ControlNotice(NoticeKind.ControlLoopError, ex.Message));
            }
            _wake.WaitOne(Interval);
        }
        RestoreOnExit();
    }

    /// <summary>One control step. Internal so tests can drive the loop deterministically.</summary>
    internal void Tick()
    {
        while (_work.TryDequeue(out var job))
            job();

        var now = _clock();
        var reapply = false;
        lock (_reapplyAt)
        {
            if (_reapplyAt.RemoveAll(t => t <= now) > 0)
                reapply = true;
        }

        ControlProfile profile;
        lock (_gate)
            profile = _profile;

        // Profile changes wake the loop early; don't hammer the sensors when that happens.
        if (now - _lastSample >= Interval * 0.8)
        {
            Sample();
            _lastSample = now;
        }
        var onAc = _power.Read().OnAc;
        _limit = ReadPowerLimit(now, onAc);

        ApplyOperatingMode(profile, reapply);
        ApplyCoolBoost(profile, reapply);
        ApplyFanTable(profile, reapply);
        // After the mode: the offsets follow the mode in force (Balanced's, on battery).
        var gpuClocks = _gpuClocks?.Step(profile.GpuClocks.For(Capabilities.HasOperatingModes, _appliedMode, Capabilities.EcHid?.GpuOffsets),
            _gpuOn, reapply);
        TrackDustDefender(now);
        var (effective, fanLock, boosting) = ApplyFans(profile, now, reapply);

        var telemetry = new Telemetry
        {
            Timestamp = now,
            CpuTemperature = _cpuTemp,
            CpuTemperatureOrigin = _cpuOrigin,
            GpuTemperature = _gpuTemp,
            GpuTemperatureOrigin = _gpuOrigin,
            GpuAsleep = _gpuAsleep,
            Limits = _limits,
            SystemTemperature = _systemTemp,
            CpuLoad = _cpuLoad,
            GpuLoad = _gpuLoad,
            // Fans OpenSense can't drive are always on the firmware's own control.
            Fans = Capabilities.Fans.Select(f => new FanTelemetry(
                f.Id,
                _rpm.GetValueOrDefault(f.Id),
                _appliedBehavior?.GetValueOrDefault(f.Id, FanBehavior.Auto) ?? FanBehavior.Auto,
                _appliedPercent.TryGetValue(f.Id, out var p) ? p : null)).ToList(),
            RequestedMode = profile.Mode,
            EffectiveMode = effective,
            FanLock = fanLock,
            OperatingMode = _appliedMode,
            CoolBoost = _appliedCoolBoost,
            OnAcPower = onAc,
            PowerLimit = _limit,
            Failsafe = _failsafe,
            GpuClocks = gpuClocks,
            DustDefenderRunning = Capabilities.DustDefender ? _dustSince is not null : null,
        };
        Latest = telemetry;
        TelemetryUpdated?.Invoke(telemetry);
    }

    private void Sample()
    {
        // Prefer the chips' own sensors; the embedded controller's copy lags and is rounded.
        var cpu = _direct.Cpu?.Read();
        _cpuOrigin = cpu is null ? TemperatureOrigin.Firmware : TemperatureOrigin.Processor;
        _cpuTemp = cpu ?? (Capabilities.Has(SensorId.CpuTemperature) && _device.ReadSensor(SensorId.CpuTemperature) is > 0 and var c ? c : null);

        var firmwareGpu = Capabilities.Has(SensorId.GpuTemperature);
        if (firmwareGpu || _direct.Gpu is not null)
        {
            var g = firmwareGpu ? _device.ReadSensor(SensorId.GpuTemperature) : null;
            // Is the discrete GPU on? Windows knows where it powers the GPU down itself. The firmware reads 0 while
            // the GPU is off, but after a resume from sleep it can go on reading 0 with the GPU back on (AN515-57).
            var on = _direct.GpuPower?.IsOn() ?? (g is { } firmware ? firmware > 0 : null);
            _gpuOn = on;
            // Asking the driver while the GPU is off could wake it.
            var gpu = on == true ? _direct.Gpu?.Read() : null;
            _gpuOrigin = gpu is null ? TemperatureOrigin.Firmware : TemperatureOrigin.GpuDriver;
            _gpuTemp = gpu ?? (g > 0 ? g : null);
            _gpuAsleep = on == false && _gpuTemp is null;
        }
        // After the readings, which is when the sensors learn their chips' limits.
        _limits = _direct.Limits;

        _systemTemp = Capabilities.Has(SensorId.SystemTemperature) && _device.ReadSensor(SensorId.SystemTemperature) is > 0 and var s ? s : null;

        // Only the RPM is real: the firmware's speed read-back echoes the last boost written.
        foreach (var fan in Capabilities.Fans)
            _rpm[fan.Id] = _device.ReadSensor(fan.RpmSensor);

        foreach (var fan in _controllable)
        {
            if (CurveTemperature(fan.Chip) is { } t)
            {
                var (sum, count) = _curveTemps.GetValueOrDefault(fan.Id);
                _curveTemps[fan.Id] = (sum + t, count + 1);
            }
        }

        (_cpuLoad, _gpuLoad) = _load.Sample();
    }

    /// <summary>
    /// What the power supply allows. The battery-boost flag, and the adapter where the embedded controller reports it,
    /// are read on AC: at start, when the adapter changes, every 30 s, and from the flag's event.
    /// </summary>
    private PowerLimit ReadPowerLimit(DateTime now, bool onAc)
    {
        var boostHid = Capabilities.EcHid is { BatteryBoost: true } ? _device.EcHid : null;
        // Only the embedded controller's rules hold modes back for the adapter.
        var adapterHid = Capabilities.EcHid is { Adapter: true } && Capabilities.ModeRules == ModeRules.EmbeddedController ? _device.EcHid : null;
        if (onAc && Capabilities.HasOperatingModes && (Capabilities.BatteryBoostFlag || adapterHid is not null) &&
            (onAc != _lastOnAc || now >= _nextBoostRead))
        {
            if (boostHid is not null)
                _batteryBoost = boostHid.ReadStatus(EcHidStatus.BatteryBoost) is { } boost ? boost == 1 : _batteryBoost;
            else if (Capabilities.BatteryBoostFlag)
                _batteryBoost = _device.GetBatteryBoost() ?? _batteryBoost;
            if (adapterHid is not null)
            {
                _weakAdapter = OperatingModePolicy.WeakAdapter(adapterHid.ReadStatus(EcHidStatus.Adapter),
                    adapterHid.ReadStatus(EcHidStatus.ModeLimit), adapterHid.ReadStatus(EcHidStatus.UsbCAdapter));
            }
            _nextBoostRead = now + BatteryBoostInterval;
        }
        _lastOnAc = onAc;
        return OperatingModePolicy.Limit(onAc, Capabilities.BatteryBoostFlag ? _batteryBoost : null, adapterHid is not null ? _weakAdapter : null);
    }

    /// <summary>The mode for the power supply (<see cref="OperatingModePolicy.Target"/>); says so when a change of supply changed it.</summary>
    private void ApplyOperatingMode(ControlProfile profile, bool reapply)
    {
        var supplyChanged = _lastLimit is { } last && last != _limit;
        _lastLimit = _limit;
        if (!Capabilities.HasOperatingModes)
        {
            RestoreDefaultMode();
            return;
        }
        if (OperatingModePolicy.Target(profile, Capabilities.OperatingModes, _limit, Capabilities.ModeRules) is not { } target)
            return;
        if (target == _appliedMode && !reapply)
            return;

        var before = _appliedMode;
        if (_modes.Write(target))
        {
            _appliedMode = target;
            if (supplyChanged && before is not null && before != target)
                Notice?.Invoke(new ControlNotice(NoticeKind.OperatingModeChangedByPower) { OperatingMode = target, PowerLimit = _limit });
        }
        else
        {
            Notice?.Invoke(new ControlNotice(NoticeKind.OperatingModeRejected) { OperatingMode = target });
        }
    }

    /// <summary>
    /// Operating modes are off on firmware that lists them: leave it in its default mode (NitroSense's
    /// "Default", our Balanced), not whichever mode was chosen while they were on.
    /// </summary>
    private void RestoreDefaultMode()
    {
        if (_defaultModeChecked || Capabilities.FirmwareOperatingModes.Count == 0)
            return;
        _defaultModeChecked = true;
        if (_modes.Read() is { } current && current != OperatingMode.Balanced)
            _modes.Write(OperatingMode.Balanced);
    }

    private void ApplyCoolBoost(ControlProfile profile, bool reapply)
    {
        if (!Capabilities.CoolBoost || profile.CoolBoost is not { } wanted)
            return;
        if (wanted == _appliedCoolBoost && !reapply)
            return;

        if (_device.SetCoolBoost(wanted))
            _appliedCoolBoost = wanted;
        else
            Notice?.Invoke(new ControlNotice(NoticeKind.CoolBoostRejected));
    }

    private void ApplyFanTable(ControlProfile profile, bool reapply)
    {
        if (!Capabilities.FanTable || profile.FanTable is not { } wanted)
            return;
        if (wanted == _appliedFanTable && !reapply)
            return;

        if (_device.SetFanTable(wanted))
            _appliedFanTable = wanted;
        else
            Notice?.Invoke(new ControlNotice(NoticeKind.FanTableRejected));
    }

    /// <returns>The mode in force, why fan control is locked, and whether OpenSense is boosting the fans.</returns>
    private (FanControlMode Effective, FanLock? Lock, bool Boosting) ApplyFans(ControlProfile profile, DateTime now, bool reapply)
    {
        // The firmware is running the fans backwards to clear dust: leave them alone until it is done.
        if (_dustSince is not null)
            return (FanControlMode.Auto, FanLock.DustDefender, false);

        var effective = profile.Mode;
        var boosting = profile.Boosts(effective);
        FanLock? fanLock = null;

        // Quiet (NitroSense) and Eco (PredatorSense) keep the fans on Auto.
        if (_appliedMode is OperatingMode.Quiet or OperatingMode.Eco && (effective != FanControlMode.Auto || boosting))
        {
            effective = FanControlMode.Auto;
            boosting = false;
            fanLock = _appliedMode == OperatingMode.Quiet ? FanLock.QuietMode : FanLock.EcoMode;
        }

        // Failsafe: boosting without a CPU temperature is flying blind.
        if (boosting && _cpuTemp is null)
        {
            if (++_cpuMisses >= FailsafeAfterMisses && !_failsafe)
            {
                _failsafe = true;
                Notice?.Invoke(new ControlNotice(NoticeKind.FansHandedBack, Important: true));
            }
        }
        else
        {
            _cpuMisses = 0;
            _failsafe = false;
        }
        if (_failsafe)
        {
            effective = FanControlMode.Auto;
            boosting = false;
        }

        // Curves step every CurveInterval, and at once when the profile or the mode changes.
        if (effective != _curveMode)
        {
            foreach (var follower in _followers.Values)
                follower.Reset();
        }
        var curveStep = now - _lastCurveStep >= CurveInterval * 0.9 || effective != _curveMode || !ReferenceEquals(profile, _curveProfile);

        var targets = _controllable.ToDictionary(f => f.Id, f => Target(f, effective, boosting, profile, curveStep));
        if (curveStep)
        {
            _curveTemps.Clear();
            _lastCurveStep = now;
            _curveProfile = profile;
            _curveMode = effective;
        }
        var assert = reapply || (now - _lastAssert >= ReassertInterval && targets.Values.Any(t => t.Behavior == FanBehavior.Custom));

        if (_appliedBehavior is null || assert || targets.Any(t => _appliedBehavior.GetValueOrDefault(t.Key) != t.Value.Behavior))
        {
            if (_controllable.Count > 0 &&
                _device.SetFanBehavior(_controllable.Select(f => (f, targets[f.Id].Behavior)).ToArray()))
            {
                _appliedBehavior = targets.ToDictionary(t => t.Key, t => t.Value.Behavior);
                _appliedPercent.Clear();
                _touchedFans = true;
            }
        }

        foreach (var fan in _controllable)
        {
            var (behavior, percent) = targets[fan.Id];
            if (behavior != FanBehavior.Custom || percent is not { } pct)
            {
                _appliedPercent.Remove(fan.Id);
                continue;
            }
            if (!assert && _appliedPercent.TryGetValue(fan.Id, out var sent) && sent == pct)
                continue;
            if (_device.SetFanSpeed(fan, pct))
                _appliedPercent[fan.Id] = pct;
        }

        if (assert)
            _lastAssert = now;
        return (effective, fanLock, boosting);
    }

    private (FanBehavior Behavior, int? Percent) Target(FanChannel fan, FanControlMode mode, bool boosting, ControlProfile profile, bool curveStep)
    {
        if (mode == FanControlMode.Max)
            return (FanBehavior.Max, null);
        if (!boosting)
            return (FanBehavior.Auto, null);

        var manual = profile.ManualFor(fan.Id);
        var curve = mode == FanControlMode.Auto ? AntiThrottle.For(fan.Chip, _limits)
            : manual.UseCurve ? profile.CurveFor(fan.Id).Curve
            : null;
        int percent;
        if (curve is null)
        {
            _followers[fan.Id].Reset();
            percent = manual.Percent;
        }
        else
        {
            percent = FollowCurve(fan, curve, curveStep);
        }

        // The firmware only boosts in tens: send the nearest one it does, not one it would round down.
        percent = AcerProtocol.NearestFanSpeed(percent);

        // A boost of 0 % is exactly Auto: leave the fan on the firmware's own behaviour then.
        return percent > 0 ? (FanBehavior.Custom, percent) : (FanBehavior.Auto, null);
    }

    private int FollowCurve(FanChannel fan, FanCurve curve, bool curveStep)
    {
        var follower = _followers[fan.Id];
        if (!curveStep && follower.Current is { } held)
            return held;
        var temperature = _curveTemps.TryGetValue(fan.Id, out var sum) && sum.Count > 0 ? sum.Sum / sum.Count : CurveTemperature(fan.Chip);
        if (temperature is { } t)
            return follower.Update(t, curve);
        if (fan.Chip == FanChip.Gpu && _gpuAsleep)
        {
            // A sleeping GPU is cold: nothing to add.
            follower.Reset();
            return 0;
        }
        return follower.Current ?? 100;
    }

    /// <summary>
    /// What a fan's curve follows: its own chip, so GPU fans the GPU (null while it sleeps) and the CPU
    /// fan the CPU. GPU fans fall back to the CPU on laptops whose firmware reports no GPU temperature,
    /// whenever there is no reading from the GPU's driver either.
    /// </summary>
    private double? CurveTemperature(FanChip chip) => chip switch
    {
        FanChip.Gpu when Capabilities.Has(SensorId.GpuTemperature) => _gpuTemp,
        FanChip.Gpu when _gpuAsleep => null,
        FanChip.Gpu => _gpuTemp ?? _cpuTemp,
        _ => _cpuTemp,
    };

    /// <summary>Follows a Dust Defender run: checks at start whether one is going, then every few seconds while one is.</summary>
    private void TrackDustDefender(DateTime now)
    {
        if (!Capabilities.DustDefender)
            return;
        if (!_dustChecked)
        {
            // One may be going already: started before the engine, by Acer's software, or by the firmware itself.
            _dustChecked = true;
            if (_device.GetDustDefenderRunning() == true)
                DustDefenderStarted(now);
            return;
        }
        if (_dustSince is not { } since || now < _dustNextCheck)
            return;
        _dustNextCheck = now + DustDefenderPoll;
        if (now - since >= DustDefenderLongest || _device.GetDustDefenderRunning() == false)
            DustDefenderEnded();
    }

    private void DustDefenderStarted(DateTime now)
    {
        _dustSince ??= now;
        _dustNextCheck = now + DustDefenderPoll;
    }

    /// <summary>The fans are the firmware's again: send them OpenSense's settings.</summary>
    private void DustDefenderEnded()
    {
        if (_dustSince is null)
            return;
        _dustSince = null;
        ReapplyAfter(TimeSpan.Zero);
    }

    private void RestoreOnExit()
    {
        try
        {
            _gpuClocks?.Restore();
        }
        catch (Exception)
        {
            // Exiting anyway; the driver clears them when it restarts.
        }

        ControlProfile profile;
        lock (_gate)
            profile = _profile;
        if (!profile.Safety.RestoreAutoOnExit || !_touchedFans || _appliedBehavior is null ||
            _appliedBehavior.Values.All(b => b == FanBehavior.Auto))
            return;
        try
        {
            _device.SetFanBehavior(_controllable.Select(f => (f, FanBehavior.Auto)).ToArray());
        }
        catch (Exception)
        {
            // Exiting anyway; the firmware keeps its last state.
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _wake.Set();
        if (_thread.IsAlive)
            _thread.Join(TimeSpan.FromSeconds(5));
        _wake.Dispose();
    }
}
