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

/// <summary>GPU (MUX) mode. Values are the firmware encoding.</summary>
public enum GpuMode : byte
{
    Hybrid = 1,
    Discrete = 2,
}

/// <summary>Setting ids for <c>Get/SetGamingMiscSetting</c>.</summary>
public enum MiscSetting : byte
{
    GpuMode = 0x02,
    GpuModeSupport = 0x09,
    SupportedOperatingModes = 0x0A,
    OperatingMode = 0x0B,
}

public enum FanId
{
    Cpu,
    Gpu,
}
