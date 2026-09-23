using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Control;

/// <summary>Why manual fan control is unavailable.</summary>
public enum FanLock
{
    /// <summary>The Quiet operating mode keeps the fans on Auto.</summary>
    QuietMode,
}

/// <param name="Duty">Duty (%) the firmware reports it is driving the fan at, in any mode.</param>
/// <param name="CommandedPercent">Duty OpenSense last commanded (Custom / Curve only).</param>
public sealed record FanTelemetry(FanId Id, int? Rpm, int? Duty, FanBehavior Behavior, int? CommandedPercent);

/// <summary>One sample of sensors plus what the controller is doing about them.</summary>
public sealed record Telemetry
{
    public required DateTime Timestamp { get; init; }
    public double? CpuTemperature { get; init; }
    public TemperatureOrigin CpuTemperatureOrigin { get; init; }

    /// <summary>Null while the discrete GPU is powered down (see <see cref="GpuAsleep"/>).</summary>
    public double? GpuTemperature { get; init; }
    public TemperatureOrigin GpuTemperatureOrigin { get; init; }

    public bool GpuAsleep { get; init; }
    public double? SystemTemperature { get; init; }
    public double? CpuLoad { get; init; }
    public double? GpuLoad { get; init; }
    public IReadOnlyList<FanTelemetry> Fans { get; init; } = [];

    public FanControlMode RequestedMode { get; init; }
    public FanControlMode EffectiveMode { get; init; }

    /// <summary>Why manual fan control is currently unavailable, if it is.</summary>
    public FanLock? FanLock { get; init; }

    public OperatingMode? OperatingMode { get; init; }
    public bool? CoolBoost { get; init; }
    public bool OnAcPower { get; init; } = true;

    /// <summary>Temperature crossed the emergency threshold; fans forced to max.</summary>
    public bool Emergency { get; init; }

    /// <summary>Sensors stopped answering; fans handed back to firmware.</summary>
    public bool Failsafe { get; init; }

    public double? Hottest => (CpuTemperature, GpuTemperature) switch
    {
        ({ } c, { } g) => Math.Max(c, g),
        ({ } c, null) => c,
        (null, { } g) => g,
        _ => null,
    };

    public FanTelemetry? Fan(FanId id) => Fans.FirstOrDefault(f => f.Id == id);
}
