using OpenSense.Core.Hardware;

namespace OpenSense.Core.Settings;

/// <summary>
/// What the engine is in the middle of, kept next to the machine settings (state.json) so it survives a restart
/// or a crash. Not user settings.
/// </summary>
public sealed record RuntimeState
{
    public CalibrationRecord? Calibration { get; init; }

    public GpuClockRecord? GpuClocks { get; init; }

    /// <summary>The laptop's Mode key has been pressed once, so it has one (see <see cref="DeviceCapabilities.ModeKey"/>).</summary>
    public bool ModeKeySeen { get; init; }
}

/// <summary>What the discrete GPU's driver said about clock offsets when last asked (asking needs the GPU awake).</summary>
/// <param name="Device">The GPU (its PCI hardware id).</param>
/// <param name="Driver">The driver's version then, for diagnostics.</param>
/// <param name="Limits">The offsets it accepts; null when it has none for this GPU.</param>
public sealed record GpuClockRecord(string Device, string? Driver, GpuClockLimits? Limits);

/// <summary>A battery calibration OpenSense started.</summary>
/// <param name="ChargeLimitBefore">The 80 % limit was on, and is turned on again afterwards.</param>
/// <param name="PowerScheme">The power plan settings changed meanwhile; null while none are changed.</param>
public sealed record CalibrationRecord(DateTime StartedUtc, bool ChargeLimitBefore, SavedPowerScheme? PowerScheme);
