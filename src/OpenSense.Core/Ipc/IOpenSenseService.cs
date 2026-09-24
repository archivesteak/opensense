using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
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

    /// <summary>Stores new capability overrides and rebuilds the session with them (raises <see cref="Rebuilt"/>).</summary>
    Task SetOverridesAsync(CapabilityOverrides capabilityOverrides, CancellationToken cancellationToken = default);

    /// <summary>Switches the GPU (MUX) mode, effective after a restart. False if the firmware refused.</summary>
    Task<bool> SetGpuModeAsync(GpuMode mode, CancellationToken cancellationToken = default);

    /// <summary>Reads what the keyboard is set to right now.</summary>
    Task<KeyboardState?> ReadKeyboardAsync(CancellationToken cancellationToken = default);
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

    public MachineSettings Settings { get; init; } = new();

    public TemperatureSources TemperatureSources { get; init; } = TemperatureSources.None;

    public Telemetry? Latest { get; init; }
}
