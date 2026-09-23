using OpenSense.Core.Hardware;

namespace OpenSense.Core.Control;

public enum FanControlMode
{
    /// <summary>Firmware controls the fans.</summary>
    Auto,

    /// <summary>All fans at full speed.</summary>
    Max,

    /// <summary>Fixed duty per fan (or Auto per fan).</summary>
    Custom,

    /// <summary>OpenSense follows a temperature curve per fan.</summary>
    Curve,
}

public enum TemperatureSource
{
    Cpu,

    /// <summary>GPU temperature; falls back to CPU while the discrete GPU sleeps.</summary>
    Gpu,

    Hottest,
}

public sealed record ManualFanSetting(bool Auto = false, int Percent = 50);

public sealed record CurveFanSetting(FanCurve Curve, TemperatureSource Source);

public sealed record ResponseSettings
{
    /// <summary>Smoothing factor per sample while temperature rises (1 = no smoothing).</summary>
    public double RiseSmoothing { get; init; } = 0.6;

    /// <summary>Smoothing factor per sample while temperature falls.</summary>
    public double FallSmoothing { get; init; } = 0.12;

    public int HysteresisC { get; init; } = 3;

    public int MinChangePercent { get; init; } = 2;
}

public sealed record SafetySettings
{
    /// <summary>Above this temperature manual and curve modes are overridden to full speed.</summary>
    public int EmergencyTemperatureC { get; init; } = 95;

    /// <summary>Lowest duty OpenSense will command in Custom / Curve mode.</summary>
    public int MinimumPercent { get; init; }

    public bool RestoreAutoOnExit { get; init; } = true;
}

/// <summary>Everything the control loop needs to know about what the user wants.</summary>
public sealed record ControlProfile
{
    public FanControlMode Mode { get; init; } = FanControlMode.Auto;

    public IReadOnlyDictionary<FanId, ManualFanSetting> Manual { get; init; } = new Dictionary<FanId, ManualFanSetting>
    {
        [FanId.Cpu] = new(),
        [FanId.Gpu] = new(),
    };

    public IReadOnlyDictionary<FanId, CurveFanSetting> Curves { get; init; } = new Dictionary<FanId, CurveFanSetting>
    {
        [FanId.Cpu] = new(CurvePresets.Balanced, TemperatureSource.Cpu),
        [FanId.Gpu] = new(CurvePresets.Balanced, TemperatureSource.Hottest),
    };

    /// <summary>Desired CoolBoost state; null leaves the firmware setting alone.</summary>
    public bool? CoolBoost { get; init; }

    /// <summary>Desired operating mode on AC power; null leaves the firmware setting alone.</summary>
    public OperatingMode? OperatingMode { get; init; }

    public ResponseSettings Response { get; init; } = new();

    public SafetySettings Safety { get; init; } = new();

    public ManualFanSetting ManualFor(FanId fan) => Manual.GetValueOrDefault(fan) ?? new ManualFanSetting();

    public CurveFanSetting CurveFor(FanId fan) =>
        Curves.GetValueOrDefault(fan) ?? new CurveFanSetting(CurvePresets.Balanced, TemperatureSource.Cpu);
}
