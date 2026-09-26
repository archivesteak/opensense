using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Control;

/// <summary>
/// Fan modes. The firmware's Custom behaviour is not a fixed speed: it adds speed on top of Auto
/// (0 % is Auto, 100 % is full speed), so every percentage OpenSense sends is such a boost.
/// </summary>
public enum FanControlMode
{
    /// <summary>Firmware controls the fans; OpenSense boosts them near the chips' limits (<see cref="AntiThrottle"/>), unless that is turned off.</summary>
    Auto,

    /// <summary>All fans at full speed.</summary>
    Max,

    /// <summary>A boost per fan, fixed or following a curve.</summary>
    Custom,
}

/// <summary>One fan in Custom mode: a fixed boost, or the boost its curve gives.</summary>
public sealed record ManualFanSetting(int Percent = 30, bool UseCurve = false);

/// <summary>A fan's curve in Custom mode. It follows the fan's own chip: the CPU fan the CPU, GPU fans the GPU.</summary>
public sealed record CurveFanSetting(FanCurve Curve);

public sealed record SafetySettings
{
    public bool RestoreAutoOnExit { get; init; } = true;
}

/// <summary>Everything the control loop needs to know about what the user wants.</summary>
public sealed record ControlProfile
{
    public FanControlMode Mode { get; init; } = FanControlMode.Auto;

    /// <summary>Auto adds speed near the chips' limits (<see cref="AntiThrottle"/>).</summary>
    public bool AutoBoost { get; init; } = true;

    public IReadOnlyDictionary<FanId, ManualFanSetting> Manual { get; init; } = new Dictionary<FanId, ManualFanSetting>
    {
        [FanId.Cpu] = new(),
        [FanId.Gpu] = new(),
    };

    /// <summary>Custom mode's curves, used by fans set to <see cref="ManualFanSetting.UseCurve"/>.</summary>
    public IReadOnlyDictionary<FanId, CurveFanSetting> Curves { get; init; } = new Dictionary<FanId, CurveFanSetting>
    {
        [FanId.Cpu] = DefaultCurve(FanId.Cpu),
        [FanId.Gpu] = DefaultCurve(FanId.Gpu),
    };

    /// <summary>Desired CoolBoost state; null leaves the firmware setting alone.</summary>
    public bool? CoolBoost { get; init; }

    /// <summary>
    /// The embedded controller's fan curve, which Auto follows and boosts add to; null leaves the firmware setting
    /// alone.
    /// </summary>
    public FanTable? FanTable { get; init; }

    /// <summary>Desired operating mode on AC power; null leaves the firmware setting alone.</summary>
    public OperatingMode? OperatingMode { get; init; }

    /// <summary>
    /// Desired operating mode on battery (Eco, Quiet or Balanced); null keeps the AC mode where the battery allows it,
    /// else Balanced (see <see cref="OperatingModePolicy"/>).
    /// </summary>
    public OperatingMode? BatteryOperatingMode { get; init; }

    /// <summary>What the laptop's Mode key does.</summary>
    public ModeKeyAction ModeKey { get; init; } = ModeKeyAction.Cycle;

    /// <summary>The mode the Mode key goes back to when it turns Turbo off (the one before Turbo).</summary>
    public OperatingMode? TurboReturnMode { get; init; }

    /// <summary>The discrete GPU's clock offsets, by operating mode; none by default.</summary>
    public GpuClockSettings GpuClocks { get; init; } = new();

    public SafetySettings Safety { get; init; } = new();

    public ManualFanSetting ManualFor(FanId fan) => Manual.GetValueOrDefault(fan) ?? new ManualFanSetting();

    public CurveFanSetting CurveFor(FanId fan) => Curves.GetValueOrDefault(fan) ?? DefaultCurve(fan);

    /// <summary>OpenSense adds speed on top of the firmware's in <paramref name="mode"/> (as opposed to leaving Auto alone).</summary>
    public bool Boosts(FanControlMode mode) => mode == FanControlMode.Custom || (mode == FanControlMode.Auto && AutoBoost);

    /// <summary>A curve nobody has set yet: Auto's, for the usual limits (the app offers the laptop's own as a preset).</summary>
    private static CurveFanSetting DefaultCurve(FanId fan) => new(AntiThrottle.For(FanChannel.Get(fan).Chip, ThermalLimits.Default));
}
