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

    internal DirectSensors(ITemperatureSensor? cpu, ITemperatureSensor? gpu, IGpuPowerState? gpuPower, SensorStatus cpuStatus, SensorStatus gpuStatus)
    {
        Cpu = cpu;
        Gpu = gpu;
        GpuPower = gpuPower;
        CpuStatus = cpuStatus;
        GpuStatus = gpuStatus;
    }

    public ITemperatureSensor? Cpu { get; }
    public ITemperatureSensor? Gpu { get; }

    /// <summary>Whether the GPU is on, as Windows sees it; without it the firmware's reading decides.</summary>
    public IGpuPowerState? GpuPower { get; }

    /// <summary>Where CPU temperatures come from, and why when it is the fallback.</summary>
    public SensorStatus CpuStatus { get; }

    public SensorStatus GpuStatus { get; }

    /// <summary>
    /// Opens both sensors. Needs an elevated process for the CPU sensor. The discrete GPU's is NVIDIA's own library
    /// for an NVIDIA GPU, else what its driver reports to Windows (any vendor, e.g. AMD).
    /// </summary>
    public static DirectSensors Open()
    {
        SensorStatus? cpuFailure = null, gpuFailure = null;
        var cpu = CpuTemperatureSensor.TryOpen((problem, detail) => cpuFailure = SensorStatus.Failed(problem, detail));

        var adapters = GpuAdapters.TryEnumerate();
        var discrete = GpuAdapters.FindDiscrete(adapters);
        var gpu = discrete is null or { VendorId: GpuAdapter.NvidiaVendorId } ? NvmlGpuSensor.TryOpen() : null;
        if (gpu is null && discrete is not null)
            gpu = WindowsGpuSensor.TryOpen(discrete, (problem, detail) => gpuFailure = SensorStatus.Failed(problem, detail));
        else if (gpu is null)
            gpuFailure = SensorStatus.Failed(SensorProblem.NoDiscreteGpu);

        // Only needed to know when the driver may be asked. One GPU can be listed twice (e.g. again for a virtual display).
        var onlyGpu = adapters.Select(a => a.PnpHardwareId).Distinct().Count() == 1;
        var gpuPower = gpu is not null && discrete is not null ? WindowsGpuPowerState.TryOpen(discrete, onlyGpu) : null;
        return new DirectSensors(cpu, gpu, gpuPower, cpu?.Status ?? cpuFailure ?? SensorStatus.NotUsed, gpu?.Status ?? gpuFailure ?? SensorStatus.NotUsed);
    }

    public void Dispose()
    {
        Cpu?.Dispose();
        Gpu?.Dispose();
    }
}
