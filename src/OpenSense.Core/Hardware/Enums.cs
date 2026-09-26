namespace OpenSense.Core.Hardware;

/// <summary>Sensor ids understood by <c>GetGamingSysInfo</c>.</summary>
public enum SensorId : byte
{
    CpuTemperature = 1,
    CpuFanSpeed = 2,
    SystemTemperature = 3,
    SystemFanSpeed = 4,
    FrostCore = 5,
    GpuFanSpeed = 6,
    System2Temperature = 7,
    System2FanSpeed = 8,
    Gpu2FanSpeed = 9,
    GpuTemperature = 10,
    Gpu2Temperature = 11,
}

/// <summary>Per-fan behaviour as encoded by <c>SetGamingFanBehavior</c>.</summary>
public enum FanBehavior : byte
{
    Auto = 1,
    Max = 2,
    Custom = 3,
}

/// <summary>Firmware operating modes (2022+ models). Values are the firmware encoding.</summary>
public enum OperatingMode : byte
{
    Quiet = 0,
    Balanced = 1,
    Performance = 4,
    Turbo = 5,
    Eco = 6,
}

/// <summary>
/// The embedded controller's fan curves, set with <c>SetGamingFanTable</c>. NitroSense picks them with its GPU overclock
/// level (normal 1, faster 2, turbo 3); firmware where nothing has picked one reads 0. Settings store these by name.
/// </summary>
public enum FanTable : byte
{
    Standard = 1,
    Faster = 2,
    Fastest = 3,
}

/// <summary>GPU (MUX) mode. Values are the firmware encoding.</summary>
public enum GpuMode : byte
{
    Hybrid = 1,
    Discrete = 2,
}

/// <summary>How asking the firmware for a Dust Defender run went.</summary>
public enum DustDefenderStart
{
    Started,

    /// <summary>The embedded controller turned it down for now (Acer: it is off under heavy load).</summary>
    Busy,

    /// <summary>A run is going already.</summary>
    Running,

    Failed,
}

/// <summary>Power-off USB charging as the firmware has it: on or off, and the battery level it stops at (null: none set).</summary>
public readonly record struct UsbChargingState(bool On, int? Floor);

/// <summary>Setting ids for <c>Get/SetGamingMiscSetting</c>.</summary>
public enum MiscSetting : byte
{
    GpuMode = 0x02,

    /// <summary>The firmware's animation and sound at power-on: 1 on, 0 off.</summary>
    BootAnimation = 0x06,

    /// <summary>
    /// The BIOS setup's "Customize POST animation": 1 shows the picture in <c>\EFI\OEM</c> at power-on, 0 Acer's logo
    /// (AN515-57 V1.17; ids the firmware doesn't have answer 0xFF).
    /// </summary>
    CustomBootLogo = 0x08,

    GpuModeSupport = 0x09,
    SupportedOperatingModes = 0x0A,
    OperatingMode = 0x0B,
}

/// <summary>The fans a model can have. Settings store these by name; add new ones at the end.</summary>
public enum FanId
{
    Cpu,
    Gpu,
    Gpu2,
    System,
    System2,
}

/// <summary>What a fan cools, and so which temperature its curve follows.</summary>
public enum FanChip
{
    Cpu,
    Gpu,
    System,
}
