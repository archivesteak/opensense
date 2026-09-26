using OpenSense.Core.Hardware;

namespace OpenSense.Core.Monitoring;

/// <summary>Where a temperature came from.</summary>
public enum TemperatureOrigin
{
    /// <summary>The embedded controller over Acer's WMI interface (what NitroSense shows).</summary>
    Firmware,

    /// <summary>The CPU's own thermal sensor, via PawnIO.</summary>
    Processor,

    /// <summary>The GPU driver: NVIDIA's library, or what the driver reports to Windows.</summary>
    GpuDriver,
}

/// <summary>
/// CPU and GPU temperatures read from the chips themselves, as ThrottleStop and HWiNFO do, instead of the embedded
/// controller's slower, rounded copy. Either sensor may be missing; callers then fall back to the firmware reading.
/// </summary>
public sealed class DirectSensors : IDisposable
{
    public static DirectSensors None { get; } = new(null, null, null, SensorStatus.NotUsed, SensorStatus.NotUsed);

    private readonly IDisposable? _library;
    private readonly int _cpuDefaultLimit;

    /// <param name="cpuDefaultLimit">The CPU's limit when its sensor doesn't report one: Intel's usual TjMax unless given.</param>
    internal DirectSensors(ITemperatureSensor? cpu, ITemperatureSensor? gpu, IGpuPowerState? gpuPower, SensorStatus cpuStatus, SensorStatus gpuStatus,
        IGpuClockControl? gpuClocks = null, IDisposable? library = null, int cpuDefaultLimit = ThermalLimits.IntelCpuDefault)
    {
        Cpu = cpu;
        Gpu = gpu;
        GpuPower = gpuPower;
        CpuStatus = cpuStatus;
        GpuStatus = gpuStatus;
        GpuClocks = gpuClocks;
        _library = library;
        _cpuDefaultLimit = cpuDefaultLimit;
    }

    public ITemperatureSensor? Cpu { get; }
    public ITemperatureSensor? Gpu { get; }

    /// <summary>Whether the GPU is on, as Windows sees it; without it the firmware's reading decides.</summary>
    public IGpuPowerState? GpuPower { get; }

    /// <summary>The NVIDIA GPU's clock offsets, where its driver has them.</summary>
    public IGpuClockControl? GpuClocks { get; }

    /// <summary>Where CPU temperatures come from, and why when it is the fallback.</summary>
    public SensorStatus CpuStatus { get; }

    public SensorStatus GpuStatus { get; }

    /// <summary>Where the chips start slowing down, as far as their sensors have said so by now.</summary>
    public ThermalLimits Limits => new(Cpu?.Limit ?? _cpuDefaultLimit, Gpu?.Limit ?? ThermalLimits.GpuDefault);

    /// <summary>
    /// Opens both sensors. Needs an elevated process for the CPU sensor. The discrete GPU's is NVIDIA's own library
    /// for an NVIDIA GPU, else what its driver reports to Windows (any vendor, e.g. AMD). NVIDIA's library also
    /// gives the GPU's clock offsets.
    /// </summary>
    public static DirectSensors Open()
    {
        SensorStatus? cpuFailure = null, gpuFailure = null;
        var cpu = CpuTemperatureSensor.TryOpen((problem, detail) => cpuFailure = SensorStatus.Failed(problem, detail));

        var adapters = GpuAdapters.TryEnumerate();
        var discrete = GpuAdapters.FindDiscrete(adapters);
        var nvml = discrete is null or { VendorId: GpuAdapter.NvidiaVendorId } ? NvmlLibrary.TryOpen(discrete) : null;
        ITemperatureSensor? gpu = nvml is null ? null : NvmlGpuSensor.TryOpen(nvml);
        var clocks = nvml is null ? null : NvmlGpuClocks.TryOpen(nvml, discrete?.PnpHardwareId ?? "");
        if (gpu is null && discrete is not null)
            gpu = WindowsGpuSensor.TryOpen(discrete, (problem, detail) => gpuFailure = SensorStatus.Failed(problem, detail));
        else if (gpu is null)
            gpuFailure = SensorStatus.Failed(SensorProblem.NoDiscreteGpu);

        // Only needed to know when the driver may be asked. One GPU can be listed twice (e.g. again for a virtual display).
        var onlyGpu = adapters.Select(a => a.PnpHardwareId).Distinct().Count() == 1;
        var gpuPower = gpu is not null && discrete is not null ? WindowsGpuPowerState.TryOpen(discrete, onlyGpu) : null;
        return new DirectSensors(cpu, gpu, gpuPower, cpu?.Status ?? cpuFailure ?? SensorStatus.NotUsed, gpu?.Status ?? gpuFailure ?? SensorStatus.NotUsed,
            clocks, nvml, CpuTemperatureSensor.DefaultLimit());
    }

    public void Dispose()
    {
        Cpu?.Dispose();
        Gpu?.Dispose();
        _library?.Dispose();
    }
}
