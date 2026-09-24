namespace OpenSense.Core.Monitoring;

/// <summary>A chip's own temperature sensor.</summary>
public enum ChipSensor
{
    /// <summary>The Intel CPU's package sensor, via PawnIO.</summary>
    IntelPackage,

    /// <summary>The AMD CPU's Tctl sensor, via PawnIO.</summary>
    AmdTctl,

    /// <summary>The NVIDIA driver (NVML).</summary>
    NvidiaDriver,

    /// <summary>The GPU's driver, through Windows (what Task Manager shows).</summary>
    GraphicsDriver,
}

/// <summary>Why a chip's own sensor is not used, so the embedded controller's reading is.</summary>
public enum SensorProblem
{
    None,
    NotX86Cpu,

    /// <summary>The CPU vendor is neither Intel nor AMD (the detail is its CPUID vendor string).</summary>
    UnsupportedCpuVendor,

    NoPackageSensor,

    /// <summary>The detail is the TjMax the CPU reported, in °C.</summary>
    ImplausibleTjMax,

    /// <summary>The sensor opened but gave no reading.</summary>
    NoReading,

    /// <summary>The detail is the error.</summary>
    PciLockUnavailable,

    NoDiscreteGpu,

    /// <summary>Windows' graphics kernel would not open the GPU's adapter; the detail is the error code.</summary>
    GpuAdapterUnavailable,

    PawnIONotInstalled,

    /// <summary>The detail is the error code.</summary>
    PawnIOUnavailable,

    /// <summary>The detail is the module and the error code.</summary>
    PawnIOModuleRefused,
}

/// <summary>Where one chip's temperature comes from.</summary>
/// <param name="Sensor">The chip's own sensor, or null when the embedded controller's reading is used.</param>
/// <param name="TjMax">For <see cref="ChipSensor.IntelPackage"/>: the temperature (°C) its readout counts down from.</param>
/// <param name="Problem">Why the chip's own sensor is not used.</param>
/// <param name="Detail">What <paramref name="Problem"/> refers to (a vendor, an error code), shown as it is.</param>
public sealed record SensorStatus(ChipSensor? Sensor, int? TjMax = null, SensorProblem Problem = SensorProblem.None, string? Detail = null)
{
    /// <summary>No direct sensor was tried; the embedded controller's reading is used.</summary>
    public static SensorStatus NotUsed { get; } = new((ChipSensor?)null);

    public static SensorStatus Failed(SensorProblem problem, string? detail = null) => new(null, null, problem, detail);
}
