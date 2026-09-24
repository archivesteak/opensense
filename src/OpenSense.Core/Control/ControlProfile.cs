using OpenSense.Core.Hardware;

namespace OpenSense.Core.Control;

/// <summary>
/// Fan modes. The firmware's Custom behaviour is not a fixed speed: it adds speed on top of Auto
/// (0 % is Auto, 100 % is full speed), so every percentage OpenSense sends is such a boost.
/// </summary>
public enum FanControlMode
{
    /// <summary>Firmware controls the fans; OpenSense boosts them when hot (<see cref="ControlProfile.AutoBoostCurve"/>), unless that is turned off.</summary>
    Auto,

    /// <summary>All fans at full speed.</summary>
    Max,

    /// <summary>A boost per fan, fixed or following a curve.</summary>
    Custom,
}

/// <summary>One fan in Custom mode: a fixed boost, or the boost its curve gives.</summary>
public sealed record ManualFanSetting(int Percent = 30, bool UseCurve = false);

/// <summary>A fan's curve in Custom mode. It follows the fan's own chip: the CPU fan the CPU, the GPU fan the GPU.</summary>
public sealed record CurveFanSetting(FanCurve Curve);

public sealed record SafetySettings
{
    public bool RestoreAutoOnExit { get; init; } = true;
}

/// <summary>Everything the control loop needs to know about what the user wants.</summary>
public sealed record ControlProfile
{
    public FanControlMode Mode { get; init; } = FanControlMode.Auto;

    /// <summary>Auto adds speed along <see cref="AutoBoostCurve"/> when it gets hot.</summary>
    public bool AutoBoost { get; init; } = true;

    public IReadOnlyDictionary<FanId, ManualFanSetting> Manual { get; init; } = new Dictionary<FanId, ManualFanSetting>
    {
        [FanId.Cpu] = new(),
        [FanId.Gpu] = new(),
    };

    /// <summary>Custom mode's curves, used by fans set to <see cref="ManualFanSetting.UseCurve"/>.</summary>
    public IReadOnlyDictionary<FanId, CurveFanSetting> Curves { get; init; } = new Dictionary<FanId, CurveFanSetting>
    {
        [FanId.Cpu] = new(CurvePresets.Default),
        [FanId.Gpu] = new(CurvePresets.Default),
    };

    /// <summary>Desired CoolBoost state; null leaves the firmware setting alone.</summary>
    public bool? CoolBoost { get; init; }

    /// <summary>Desired operating mode on AC power; null leaves the firmware setting alone.</summary>
    public OperatingMode? OperatingMode { get; init; }

    public SafetySettings Safety { get; init; } = new();

    public ManualFanSetting ManualFor(FanId fan) => Manual.GetValueOrDefault(fan) ?? new ManualFanSetting();

    public CurveFanSetting CurveFor(FanId fan) =>
        Curves.GetValueOrDefault(fan) ?? new CurveFanSetting(CurvePresets.Default);

    /// <summary>OpenSense adds speed on top of the firmware's in <paramref name="mode"/> (as opposed to leaving Auto alone).</summary>
    public bool Boosts(FanControlMode mode) => mode == FanControlMode.Custom || (mode == FanControlMode.Auto && AutoBoost);

    /// <summary>Auto's fixed boost, for every fan on its own chip's temperature.</summary>
    public static FanCurve AutoBoostCurve => CurvePresets.Default;
}
