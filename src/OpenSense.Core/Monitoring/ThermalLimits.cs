namespace OpenSense.Core.Monitoring;

/// <summary>
/// Where the chips start slowing themselves down because they are hot (°C): each as the chip reports it where it does
/// (<see cref="ITemperatureSensor.Limit"/>), else the usual value for its kind.
/// </summary>
/// <param name="Cpu">Intel: TjMax less the TCC offset, if one is set. AMD's processors don't report theirs.</param>
/// <param name="Gpu">NVIDIA: the temperature GPU Boost holds the GPU under by lowering its clocks (the target temperature nvidia-smi lists).</param>
public sealed record ThermalLimits(int Cpu, int Gpu)
{
    /// <summary>TjMax of most of Intel's H and HX processors.</summary>
    public const int IntelCpuDefault = 100;

    /// <summary>AMD's mobile Ryzen processors stop at 95 to 105 °C, by generation, and don't report where: the lowest.</summary>
    public const int AmdCpuDefault = 95;

    /// <summary>NVIDIA's rated maximum for its RTX laptop GPUs, also used for GPUs whose driver doesn't say.</summary>
    public const int GpuDefault = 87;

    public static ThermalLimits Default { get; } = new(IntelCpuDefault, GpuDefault);
}
