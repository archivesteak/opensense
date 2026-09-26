using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Control;

/// <summary>Why manual fan control is unavailable.</summary>
public enum FanLock
{
    /// <summary>The Quiet operating mode keeps the fans on Auto.</summary>
    QuietMode,

    /// <summary>The Eco operating mode keeps the fans on Auto (as Acer's software has it).</summary>
    EcoMode,

    /// <summary>The firmware is running the fans backwards to clear dust.</summary>
    DustDefender,
}

/// <param name="BoostPercent">Boost OpenSense is adding on top of Auto; null while the fan is left to the firmware.</param>
/// <remarks>The firmware reports no duty: its speed read-back only echoes the last boost written, so RPM is the measure.</remarks>
public sealed record FanTelemetry(FanId Id, int? Rpm, FanBehavior Behavior, int? BoostPercent);

/// <summary>The discrete GPU's clock offsets, as OpenSense controls them.</summary>
/// <param name="Limits">What the driver accepts; null until the GPU has been on once (then remembered across restarts).</param>
/// <param name="Applied">The offsets in the driver, as last read or written; null while the GPU is off.</param>
/// <param name="Target">What OpenSense sets in the operating mode in force, within the limits; null while it leaves the clocks alone.</param>
public sealed record GpuClockTelemetry(GpuClockLimits? Limits, ClockOffsets? Applied, ClockOffsets? Target);

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

    /// <summary>Where the chips start slowing down, which Auto's boost goes by (<see cref="AntiThrottle"/>).</summary>
    public ThermalLimits Limits { get; init; } = ThermalLimits.Default;

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

    /// <summary>Which operating modes the power supply allows now (see <see cref="OperatingModePolicy.Allowed"/>).</summary>
    public PowerLimit PowerLimit { get; init; }

    /// <summary>Sensors stopped answering; fans handed back to firmware.</summary>
    public bool Failsafe { get; init; }

    /// <summary>Null on machines without a battery.</summary>
    public BatteryTelemetry? Battery { get; init; }

    /// <summary>Null without an NVIDIA GPU whose driver takes clock offsets.</summary>
    public GpuClockTelemetry? GpuClocks { get; init; }

    /// <summary>A Dust Defender run is going; null on laptops without it.</summary>
    public bool? DustDefenderRunning { get; init; }

    public double? Hottest => (CpuTemperature, GpuTemperature) switch
    {
        ({ } c, { } g) => Math.Max(c, g),
        ({ } c, null) => c,
        (null, { } g) => g,
        _ => null,
    };

    public FanTelemetry? Fan(FanId id) => Fans.FirstOrDefault(f => f.Id == id);
}
