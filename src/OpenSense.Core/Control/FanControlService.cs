using System.Collections.Concurrent;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Control;

/// <summary>
/// Owns the firmware: polls sensors, applies the user's <see cref="ControlProfile"/>, runs fan curves
/// and enforces safety rules. All WMI traffic happens on its single worker thread.
/// Fan speeds are boosts on top of the firmware's Auto (see <see cref="AcerProtocol.FanSpeedInput"/>).
/// </summary>
public sealed class FanControlService : IDeviceDispatcher, IDisposable
{
    private static readonly TimeSpan ReassertInterval = TimeSpan.FromSeconds(30);

    /// <summary>Curves are re-evaluated this often, on the average temperature since the last time.</summary>
    internal static readonly TimeSpan CurveInterval = TimeSpan.FromSeconds(3);
    private const int FailsafeAfterMisses = 3;

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
    private DateTime _lastAssert = DateTime.MinValue;
    private bool _touchedFans;
    private bool _defaultModeChecked;

    // Safety state.
    private bool _failsafe;
    private int _cpuMisses;

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

    /// <summary>How often sensors are read: fixed at a second (curves act every <see cref="CurveInterval"/>); tests shorten it.</summary>
    public TimeSpan Interval { get; internal set; } = TimeSpan.FromSeconds(1);

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
            Sample();
            _lastSample = now;
        }
        var onAc = _power.IsOnAcPower;

        ApplyOperatingMode(profile, onAc, reapply);
        ApplyCoolBoost(profile, reapply);
        var (effective, fanLock, boosting) = ApplyFans(profile, now, reapply);

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
                _appliedBehavior?.GetValueOrDefault(f.Id, FanBehavior.Auto) ?? FanBehavior.Auto,
                _appliedPercent.TryGetValue(f.Id, out var p) ? p : null)).ToList(),
            RequestedMode = profile.Mode,
            EffectiveMode = effective,
            FanLock = fanLock,
            OperatingMode = _appliedMode,
            CoolBoost = _appliedCoolBoost,
            OnAcPower = onAc,
            Failsafe = _failsafe,
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

        if (Capabilities.Has(SensorId.GpuTemperature))
        {
            var g = _device.ReadSensor(SensorId.GpuTemperature);
            _gpuAsleep = g == 0;
            // The firmware reads 0 while the discrete GPU is powered down; asking the driver then could wake it.
            var gpu = g > 0 ? _direct.Gpu?.Read() : null;
            _gpuOrigin = gpu is null ? TemperatureOrigin.Firmware : TemperatureOrigin.GpuDriver;
            _gpuTemp = gpu ?? (g > 0 ? g : null);
        }

        _systemTemp = Capabilities.Has(SensorId.SystemTemperature) && _device.ReadSensor(SensorId.SystemTemperature) is > 0 and var s ? s : null;

        // Only the RPM is real: the firmware's speed read-back echoes the last boost written.
        foreach (var fan in Capabilities.Fans)
            _rpm[fan.Id] = _device.ReadSensor(fan.RpmSensor);

        foreach (var fan in Capabilities.Fans)
        {
            if (CurveTemperature(fan.Id) is { } t)
            {
                var (sum, count) = _curveTemps.GetValueOrDefault(fan.Id);
                _curveTemps[fan.Id] = (sum + t, count + 1);
            }
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

    /// <returns>The mode in force, why fan control is locked, and whether OpenSense is boosting the fans.</returns>
    private (FanControlMode Effective, FanLock? Lock, bool Boosting) ApplyFans(ControlProfile profile, DateTime now, bool reapply)
    {
        var effective = profile.Mode;
        var boosting = profile.Boosts(effective);
        FanLock? fanLock = null;

        if (_appliedMode == OperatingMode.Quiet && (effective != FanControlMode.Auto || boosting))
        {
            effective = FanControlMode.Auto;
            boosting = false;
            fanLock = FanLock.QuietMode;
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

        var targets = Capabilities.Fans.ToDictionary(f => f.Id, f => Target(f, effective, boosting, profile, curveStep));
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
        return (effective, fanLock, boosting);
    }

    private (FanBehavior Behavior, int? Percent) Target(FanChannel fan, FanControlMode mode, bool boosting, ControlProfile profile, bool curveStep)
    {
        if (mode == FanControlMode.Max)
            return (FanBehavior.Max, null);
        if (!boosting)
            return (FanBehavior.Auto, null);

        var manual = profile.ManualFor(fan.Id);
        var curve = mode == FanControlMode.Auto ? ControlProfile.AutoBoostCurve
            : manual.UseCurve ? profile.CurveFor(fan.Id).Curve
            : null;
        int percent;
        if (curve is null)
        {
            _followers[fan.Id].Reset();
            percent = Math.Clamp(manual.Percent, 0, 100);
        }
        else
        {
            percent = FollowCurve(fan.Id, curve, profile, curveStep);
        }

        // A boost of 0 % is exactly Auto: leave the fan on the firmware's own behaviour then.
        return percent > 0 ? (FanBehavior.Custom, percent) : (FanBehavior.Auto, null);
    }

    private int FollowCurve(FanId fan, FanCurve curve, ControlProfile profile, bool curveStep)
    {
        var follower = _followers[fan];
        if (!curveStep && follower.Current is { } held)
            return held;
        var temperature = _curveTemps.TryGetValue(fan, out var sum) && sum.Count > 0 ? sum.Sum / sum.Count : CurveTemperature(fan);
        if (temperature is { } t)
            return follower.Update(t, curve);
        if (fan == FanId.Gpu && _gpuAsleep)
        {
            // A sleeping GPU is cold: nothing to add.
            follower.Reset();
            return 0;
        }
        return follower.Current ?? 100;
    }

    /// <summary>
    /// What a fan's curve follows: its own chip, so the GPU fan the GPU (null while it sleeps) and the CPU
    /// fan the CPU. The GPU fan falls back to the CPU on laptops whose firmware reports no GPU temperature.
    /// </summary>
    private double? CurveTemperature(FanId fan) =>
        fan == FanId.Gpu && Capabilities.Has(SensorId.GpuTemperature) ? _gpuTemp : _cpuTemp;

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
