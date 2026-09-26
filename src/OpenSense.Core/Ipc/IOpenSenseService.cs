using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;
using OpenSense.Core.Lighting;
using OpenSense.Core.Monitoring;
using OpenSense.Core.Settings;
using System.Text.Json.Serialization;
using StreamJsonRpc;

namespace OpenSense.Core.Ipc;

/// <summary>
/// What the OpenSense engine offers its clients. The app reaches it over the service's named pipe
/// (<see cref="OpenSensePipe"/>); a portable copy hosts the engine itself.
/// </summary>
[JsonRpcContract]
public partial interface IOpenSenseService
{
    /// <summary>Raised after every sensor sample.</summary>
    event EventHandler<Telemetry>? TelemetryUpdated;

    /// <summary>Something the user should know about, e.g. the firmware rejected a change.</summary>
    event EventHandler<ControlNotice>? NoticeRaised;

    /// <summary>The device session was rebuilt (capability overrides changed); carries the new state.</summary>
    event EventHandler<EngineSnapshot>? Rebuilt;

    /// <summary>The machine settings changed (from any client), so every client can stay in step.</summary>
    event EventHandler<MachineSettings>? SettingsChanged;

    Task<EngineSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task SetProfileAsync(ControlProfile profile, CancellationToken cancellationToken = default);

    Task SetKeyboardAsync(KeyboardSettings keyboard, CancellationToken cancellationToken = default);

    /// <summary>What every light should show (the lights not listed are left as they are).</summary>
    Task SetLightingAsync(LightingConfig lighting, CancellationToken cancellationToken = default);

    /// <summary>Stores new capability overrides and rebuilds the session with them (raises <see cref="Rebuilt"/>).</summary>
    Task SetOverridesAsync(CapabilityOverrides capabilityOverrides, CancellationToken cancellationToken = default);

    /// <summary>Switches the GPU (MUX) mode, effective after a restart. False if the firmware refused.</summary>
    Task<bool> SetGpuModeAsync(GpuMode mode, CancellationToken cancellationToken = default);

    /// <summary>Has the firmware run the fans backwards for a moment to blow the dust out (Dust Defender).</summary>
    Task<DustDefenderStart> StartDustDefenderAsync(CancellationToken cancellationToken = default);

    /// <summary>Battery charge limit and power-off USB charging.</summary>
    Task SetPowerAsync(PowerSettings power, CancellationToken cancellationToken = default);

    /// <summary>Starts a battery calibration (hours long; the adapter must stay plugged in).</summary>
    Task<CalibrationResult> StartBatteryCalibrationAsync(CancellationToken cancellationToken = default);

    Task StopBatteryCalibrationAsync(CancellationToken cancellationToken = default);

    /// <summary>How worn the battery is, as it reports itself to Windows; null without a battery.</summary>
    Task<BatteryHealth?> ReadBatteryHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Turns the boot animation and sound on or off from the next start. False if the firmware refused.</summary>
    Task<bool> SetBootAnimationAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>The custom boot logo in use, and the screen a new one must fit.</summary>
    Task<BootLogoState> GetBootLogoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks <paramref name="image"/> (the file's bytes: the service never opens a path a client names) against Acer's
    /// rules and makes it the boot logo.
    /// </summary>
    Task<BootLogoResult> SetBootLogoAsync(byte[] image, CancellationToken cancellationToken = default);

    /// <summary>Removes the custom boot logo, so Acer's shows again.</summary>
    Task<bool> RestoreBootLogoAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads what the keyboard is set to right now.</summary>
    Task<KeyboardState?> ReadKeyboardAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads what each light shows right now, by <see cref="LightingDeviceInfo.Id"/> (the lights that can be read).</summary>
    Task<IReadOnlyDictionary<string, LightingSettings>?> ReadLightingAsync(CancellationToken cancellationToken = default);

    /// <summary>The capability probe's raw answers and the firmware's recent events, for bug reports.</summary>
    Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
}

public enum EngineState
{
    Starting,
    Ready,

    /// <summary>No Acer gaming firmware on this machine.</summary>
    Unsupported,

    Failed,
}

/// <summary>Where temperatures come from, for the UI.</summary>
public sealed record TemperatureSources(SensorStatus Cpu, SensorStatus Gpu)
{
    public static TemperatureSources None { get; } = new(SensorStatus.NotUsed, SensorStatus.NotUsed);

    /// <summary>The CPU sensor is unavailable because the PawnIO driver is not installed.</summary>
    [JsonIgnore]
    public bool PawnIOMissing => Cpu.Problem == SensorProblem.PawnIONotInstalled;
}

/// <summary>Everything a client needs to show the laptop, taken at one moment.</summary>
public sealed record EngineSnapshot
{
    public int ProtocolVersion { get; init; } = OpenSensePipe.ProtocolVersion;

    public string EngineVersion { get; init; } = "";

    public EngineState State { get; init; }

    public string? Error { get; init; }

    /// <summary>The laptop's model name, if the firmware reports one.</summary>
    public string? DeviceName { get; init; }

    public string? BiosVersion { get; init; }

    /// <summary>The serial number on the laptop's label.</summary>
    public string? SerialNumber { get; init; }

    public string? CpuName { get; init; }

    public string? GpuName { get; init; }

    /// <summary>What detection found, before overrides.</summary>
    public DeviceCapabilities Detected { get; init; } = DeviceCapabilities.None;

    /// <summary>What is in use (detection + overrides).</summary>
    public DeviceCapabilities Capabilities { get; init; } = DeviceCapabilities.None;

    /// <summary>The firmware's state when the engine started; the GPU mode follows later switches.</summary>
    public FirmwareState? Firmware { get; init; }

    /// <summary>The keyboard's state when the engine started.</summary>
    public KeyboardState? Keyboard { get; init; }

    /// <summary>What the lights showed when the engine started, by <see cref="LightingDeviceInfo.Id"/>.</summary>
    public IReadOnlyDictionary<string, LightingSettings>? Lighting { get; init; }

    public MachineSettings Settings { get; init; } = new();

    public TemperatureSources TemperatureSources { get; init; } = TemperatureSources.None;

    public Telemetry? Latest { get; init; }
}
