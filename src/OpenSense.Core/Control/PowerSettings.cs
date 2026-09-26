namespace OpenSense.Core.Control;

/// <summary>Battery and USB power. The firmware keeps these itself; null leaves what it has.</summary>
public sealed record PowerSettings
{
    /// <summary>Charging stops at 80 %.</summary>
    public bool? ChargeLimit { get; init; }

    public UsbChargingSettings? UsbCharging { get; init; }
}

/// <summary>Power-off USB charging: on or off, and the battery level (10, 20 or 30 %) it stops at.</summary>
public sealed record UsbChargingSettings(bool On, int Floor);

/// <summary>How a request to calibrate the battery went.</summary>
public enum CalibrationResult
{
    Started,

    /// <summary>The adapter has to be plugged in.</summary>
    NeedsAc,

    AlreadyRunning,
    Unsupported,

    /// <summary>The firmware refused it.</summary>
    Rejected,
}

/// <summary>The battery, for the UI.</summary>
/// <param name="Percent">Charge left; null when Windows does not know.</param>
/// <param name="CalibrationStarted">When the running calibration started (UTC); null while none runs.</param>
public sealed record BatteryTelemetry(int? Percent, bool? Charging, DateTime? CalibrationStarted);
