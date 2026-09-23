using System.Collections.Concurrent;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Control;

/// <summary>
/// Owns the firmware: polls sensors, applies the user's <see cref="ControlProfile"/>, runs fan curves
/// and enforces safety rules. All WMI traffic happens on its single worker thread.
/// </summary>
public sealed class FanControlService : IDeviceDispatcher, IDisposable
{
    private static readonly TimeSpan ReassertInterval = TimeSpan.FromSeconds(30);
    private const int FailsafeAfterMisses = 3;
    private const int EmergencyReleaseMarginC = 8;
    private const int EmergencyAfterSamples = 2;

    private readonly AcerDevice _device;
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
    private TemperatureOrigin _cpuOrigin, _gpuOrigin;
    private readonly Dictionary<FanId, int?> _rpm = new();
    private readonly Dictionary<FanId, int?> _duty = new();

    // What the firmware was last told.
    private Dictionary<FanId, FanBehavior>? _appliedBehavior;
    private readonly Dictionary<FanId, int> _appliedPercent = new();
    private OperatingMode? _appliedMode;
    private bool? _appliedCoolBoost;
    private DateTime _lastAssert = DateTime.MinValue;
    private bool _touchedFans;
    private bool _defaultModeChecked;

    // Safety state.
    private bool _emergency;
    private bool _failsafe;
    private int _cpuMisses;
    private int _hotSamples;

    public FanControlService(AcerDevice device, DeviceCapabilities capabilities, ILoadMonitor load, IPowerSource power,
        ControlProfile initialProfile, DirectSensors? direct = null, Func<DateTime>? clock = null)
    {
        _device = device;
        Capabilities = capabilities;
        _load = load;
        _power = power;
        _direct = direct ?? DirectSensors.None;
        _profile = initialProfile;
        _clock = clock ?? (() => DateTime.UtcNow);
        foreach (var fan in capabilities.Fans)
            _followers[fan.Id] = new CurveFollower();
        _thread = new Thread(Run) { IsBackground = true, Name = "OpenSense control loop" };
    }

    public DeviceCapabilities Capabilities { get; }

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    public Telemetry? Latest { get; private set; }

    /// <summary>Raised on the control thread after every sample.</summary>
    public event Action<Telemetry>? TelemetryUpdated;

    /// <summary>Raised on the control thread for things the user should know about.</summary>
    public event Action<ControlNotice>? Notice;

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
    public void OnResume() => ReapplyAfter(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(12));

    /// <summary>AC power was connected or removed; re-evaluate the operating mode right away.</summary>
    public void OnPowerSourceChanged() => _wake.Set();

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
            Sample(profile);
            _lastSample = now;
        }
        var onAc = _power.IsOnAcPower;

        ApplyOperatingMode(profile, onAc, reapply);
        ApplyCoolBoost(profile, reapply);
        var (effective, fanLock) = ApplyFans(profile, now, reapply);

        var telemetry = new Telemetry
        {
            Timestamp = now,
            CpuTemperature = _cpuTemp,
            CpuTemperatureOrigin = _cpuOrigin,
            GpuTemperature = _gpuTemp,
            GpuTemperatureOrigin = _gpuOrigin,
            GpuAsleep = _gpuAsleep,
            SystemTemperature = _systemTemp,
            CpuLoad = _cpuLoad,
            GpuLoad = _gpuLoad,
            Fans = Capabilities.Fans.Select(f => new FanTelemetry(
                f.Id,
                _rpm.GetValueOrDefault(f.Id),
                _duty.GetValueOrDefault(f.Id),
                _appliedBehavior?.GetValueOrDefault(f.Id, FanBehavior.Auto) ?? FanBehavior.Auto,
                _appliedPercent.TryGetValue(f.Id, out var p) ? p : null)).ToList(),
            RequestedMode = profile.Mode,
            EffectiveMode = effective,
            FanLock = fanLock,
            OperatingMode = _appliedMode,
            CoolBoost = _appliedCoolBoost,
            OnAcPower = onAc,
            Emergency = _emergency && effective == FanControlMode.Max && profile.Mode is FanControlMode.Custom or FanControlMode.Curve,
            Failsafe = _failsafe,
        };
        Latest = telemetry;
        TelemetryUpdated?.Invoke(telemetry);
    }

    private void Sample(ControlProfile profile)
    {
        // Prefer the chips' own sensors; the embedded controller's copy lags and is rounded.
        var cpu = _direct.Cpu?.Read();
        _cpuOrigin = cpu is null ? TemperatureOrigin.Firmware : TemperatureOrigin.Processor;
        _cpuTemp = cpu ?? (Capabilities.Has(SensorId.CpuTemperature) && _device.ReadSensor(SensorId.CpuTemperature) is > 0 and var c ? c : null);

        if (Capabilities.Has(SensorId.GpuTemperature))
        {
            var g = _device.ReadSensor(SensorId.GpuTemperature);
            _gpuAsleep = g == 0;
            // The firmware reads 0 while the discrete GPU is powered down; asking the driver then could wake it.
            var gpu = g > 0 ? _direct.Gpu?.Read() : null;
            _gpuOrigin = gpu is null ? TemperatureOrigin.Firmware : TemperatureOrigin.GpuDriver;
            _gpuTemp = gpu ?? (g > 0 ? g : null);
        }

        // The CPU's own sensor catches sub-second turbo spikes; only sustained heat counts as an emergency.
        _hotSamples = new[] { _cpuTemp, _gpuTemp }.Max() >= profile.Safety.EmergencyTemperatureC ? _hotSamples + 1 : 0;

        _systemTemp = Capabilities.Has(SensorId.SystemTemperature) && _device.ReadSensor(SensorId.SystemTemperature) is > 0 and var s ? s : null;

        foreach (var fan in Capabilities.Fans)
        {
            _rpm[fan.Id] = _device.ReadSensor(fan.RpmSensor);
            _duty[fan.Id] = _device.GetFanDuty(fan);
        }

        (_cpuLoad, _gpuLoad) = _load.Sample();
    }

    private void ApplyOperatingMode(ControlProfile profile, bool onAc, bool reapply)
    {
        if (!Capabilities.HasOperatingModes)
        {
            RestoreDefaultMode();
            return;
        }
        if (profile.OperatingMode is not { } wanted)
            return;

        // Performance modes need AC power; on battery the machine runs Balanced.
        var target = onAc || wanted is not (OperatingMode.Performance or OperatingMode.Turbo) ? wanted : OperatingMode.Balanced;
        if (!Capabilities.OperatingModes.Contains(target))
            return;
        if (target == _appliedMode && !reapply)
            return;

        if (_device.SetOperatingMode(target))
            _appliedMode = target;
        else
            Notice?.Invoke(new ControlNotice(NoticeKind.OperatingModeRejected) { OperatingMode = target });
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
        if (_device.GetOperatingMode() is { } current && current != OperatingMode.Balanced)
            _device.SetOperatingMode(OperatingMode.Balanced);
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

    private (FanControlMode Effective, FanLock? Lock) ApplyFans(ControlProfile profile, DateTime now, bool reapply)
    {
        var effective = profile.Mode;
        FanLock? fanLock = null;

        if (_appliedMode == OperatingMode.Quiet && effective != FanControlMode.Auto)
        {
            effective = FanControlMode.Auto;
            fanLock = FanLock.QuietMode;
        }

        // Failsafe: manual control without a CPU temperature is flying blind.
        if (effective is FanControlMode.Custom or FanControlMode.Curve && _cpuTemp is null)
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
            effective = FanControlMode.Auto;

        // Emergency: never let a manual setting cook the machine.
        var hottest = new[] { _cpuTemp, _gpuTemp }.Max();
        var threshold = profile.Safety.EmergencyTemperatureC;
        if (_hotSamples >= EmergencyAfterSamples)
        {
            _emergency = true; // shown through the fans' behaviour, not as a notice
        }
        else if (_emergency && (hottest is null || hottest <= threshold - EmergencyReleaseMarginC))
        {
            _emergency = false;
        }
        if (_emergency && effective is FanControlMode.Custom or FanControlMode.Curve)
            effective = FanControlMode.Max;

        if (effective != FanControlMode.Curve)
        {
            foreach (var follower in _followers.Values)
                follower.Reset();
        }

        var targets = Capabilities.Fans.ToDictionary(f => f.Id, f => Target(f, effective, profile));
        var assert = reapply || (now - _lastAssert >= ReassertInterval && targets.Values.Any(t => t.Behavior == FanBehavior.Custom));

        if (_appliedBehavior is null || assert || targets.Any(t => _appliedBehavior.GetValueOrDefault(t.Key) != t.Value.Behavior))
        {
            if (Capabilities.Fans.Count > 0 &&
                _device.SetFanBehavior(Capabilities.Fans.Select(f => (f, targets[f.Id].Behavior)).ToArray()))
            {
                _appliedBehavior = targets.ToDictionary(t => t.Key, t => t.Value.Behavior);
                _appliedPercent.Clear();
                _touchedFans = true;
            }
        }

        foreach (var fan in Capabilities.Fans)
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
        return (effective, fanLock);
    }

    private (FanBehavior Behavior, int? Percent) Target(FanChannel fan, FanControlMode mode, ControlProfile profile)
    {
        var min = profile.Safety.MinimumPercent;
        switch (mode)
        {
            case FanControlMode.Max:
                return (FanBehavior.Max, null);
            case FanControlMode.Custom:
                var manual = profile.ManualFor(fan.Id);
                return manual.Auto ? (FanBehavior.Auto, null) : (FanBehavior.Custom, Math.Clamp(Math.Max(manual.Percent, min), 0, 100));
            case FanControlMode.Curve:
                var curve = profile.CurveFor(fan.Id);
                var follower = _followers[fan.Id];
                var temperature = SourceTemperature(curve.Source);
                var pct = temperature is { } t
                    ? follower.Update(t, curve.Curve, profile.Response, min)
                    : follower.Current ?? 100;
                return (FanBehavior.Custom, pct);
            default:
                return (FanBehavior.Auto, null);
        }
    }

    private double? SourceTemperature(TemperatureSource source) => source switch
    {
        TemperatureSource.Cpu => _cpuTemp,
        TemperatureSource.Gpu => _gpuTemp ?? _cpuTemp,
        _ => new[] { _cpuTemp, _gpuTemp }.Max(),
    };

    private void RestoreOnExit()
    {
        ControlProfile profile;
        lock (_gate)
            profile = _profile;
        if (!profile.Safety.RestoreAutoOnExit || !_touchedFans || _appliedBehavior is null ||
            _appliedBehavior.Values.All(b => b == FanBehavior.Auto))
            return;
        try
        {
            _device.SetFanBehavior(Capabilities.Fans.Select(f => (f, FanBehavior.Auto)).ToArray());
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
