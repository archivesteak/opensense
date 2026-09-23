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
    public static DirectSensors None { get; } = new(null, null, "Not used", "Not used", pawnIOMissing: false);

    private DirectSensors(ITemperatureSensor? cpu, ITemperatureSensor? gpu, string cpuStatus, string gpuStatus, bool pawnIOMissing)
    {
        Cpu = cpu;
        Gpu = gpu;
        CpuStatus = cpuStatus;
        GpuStatus = gpuStatus;
        PawnIOMissing = pawnIOMissing;
    }

    public ITemperatureSensor? Cpu { get; }
    public ITemperatureSensor? Gpu { get; }

    /// <summary>One line on where CPU temperatures come from, and why when it is the fallback.</summary>
    public string CpuStatus { get; }

    public string GpuStatus { get; }

    /// <summary>The CPU sensor is unavailable because the PawnIO driver is not installed.</summary>
    public bool PawnIOMissing { get; }

    /// <summary>Opens both sensors. Needs an elevated process for the CPU sensor.</summary>
    public static DirectSensors Open()
    {
        string? cpuProblem = null, gpuProblem = null;
        var cpu = CpuTemperatureSensor.TryOpen(message => cpuProblem = message);
        var gpu = NvmlGpuSensor.TryOpen(message => gpuProblem = message);
        return new DirectSensors(
            cpu,
            gpu,
            cpu?.Description ?? $"Embedded controller ({cpuProblem})",
            gpu?.Description ?? $"Embedded controller ({gpuProblem})",
            pawnIOMissing: cpu is null && cpuProblem == PawnIOModule.NotInstalledMessage);
    }

    public void Dispose()
    {
        Cpu?.Dispose();
        Gpu?.Dispose();
    }
}
