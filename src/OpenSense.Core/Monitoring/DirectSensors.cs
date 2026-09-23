namespace OpenSense.Core.Monitoring;

/// <summary>Where a temperature came from.</summary>
public enum TemperatureOrigin
{
    /// <summary>The embedded controller over Acer's WMI interface (what NitroSense shows).</summary>
    Firmware,

    /// <summary>The CPU's own thermal sensor, via PawnIO.</summary>
    Processor,

    /// <summary>The GPU driver.</summary>
    GpuDriver,
}

/// <summary>
/// CPU and GPU temperatures read from the chips themselves, as ThrottleStop and HWiNFO do, instead of the embedded
/// controller's slower, rounded copy. Either sensor may be missing; callers then fall back to the firmware reading.
/// </summary>
public sealed class DirectSensors : IDisposable
{
    public static DirectSensors None { get; } = new(null, null, SensorStatus.NotUsed, SensorStatus.NotUsed);

    private DirectSensors(ITemperatureSensor? cpu, ITemperatureSensor? gpu, SensorStatus cpuStatus, SensorStatus gpuStatus)
    {
        Cpu = cpu;
        Gpu = gpu;
        CpuStatus = cpuStatus;
        GpuStatus = gpuStatus;
    }

    public ITemperatureSensor? Cpu { get; }
    public ITemperatureSensor? Gpu { get; }

    /// <summary>Where CPU temperatures come from, and why when it is the fallback.</summary>
    public SensorStatus CpuStatus { get; }

    public SensorStatus GpuStatus { get; }

    /// <summary>Opens both sensors. Needs an elevated process for the CPU sensor.</summary>
    public static DirectSensors Open()
    {
        SensorStatus? cpuFailure = null, gpuFailure = null;
        var cpu = CpuTemperatureSensor.TryOpen((problem, detail) => cpuFailure = SensorStatus.Failed(problem, detail));
        var gpu = NvmlGpuSensor.TryOpen((problem, detail) => gpuFailure = SensorStatus.Failed(problem, detail));
        return new DirectSensors(cpu, gpu, cpu?.Status ?? cpuFailure ?? SensorStatus.NotUsed, gpu?.Status ?? gpuFailure ?? SensorStatus.NotUsed);
    }

    public void Dispose()
    {
        Cpu?.Dispose();
        Gpu?.Dispose();
    }
}
