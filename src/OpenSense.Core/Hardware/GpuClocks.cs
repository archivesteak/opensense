using System.Text.Json.Serialization;

namespace OpenSense.Core.Hardware;

/// <summary>
/// Offsets added to the discrete GPU's clocks, in MHz, as its driver takes them. The core offset moves the whole
/// speed curve (NVIDIA drivers in 15 MHz steps); the memory offset is in double-data-rate MHz, as MSI Afterburner
/// shows it (+100 raises the memory clock by 50 MHz).
/// </summary>
public sealed record ClockOffsets(int CoreMhz = 0, int MemoryMhz = 0)
{
    public static ClockOffsets None { get; } = new();

    [JsonIgnore]
    public bool IsNone => CoreMhz == 0 && MemoryMhz == 0;

    public ClockOffsets Add(ClockOffsets other) => new(CoreMhz + other.CoreMhz, MemoryMhz + other.MemoryMhz);
}

/// <summary>The offsets the driver accepts, in MHz.</summary>
public sealed record GpuClockLimits(int CoreMin, int CoreMax, int MemoryMin, int MemoryMax)
{
    public ClockOffsets Clamp(ClockOffsets offsets) => new(
        Math.Clamp(offsets.CoreMhz, CoreMin, CoreMax),
        Math.Clamp(offsets.MemoryMhz, MemoryMin, MemoryMax));
}

public enum GpuClock
{
    Core,
    Memory,
}

/// <summary>The offsets in force and the driver's limits.</summary>
public sealed record GpuClockReading(ClockOffsets Offsets, GpuClockLimits Limits);

/// <summary>
/// The discrete GPU's clock offsets, through its driver. Only to be used while the GPU is on: asking the driver
/// powers up a sleeping (Optimus) GPU. Writing needs administrator rights, which the engine has.
/// </summary>
public interface IGpuClockControl
{
    /// <summary>The GPU this is for (its PCI hardware id), known without asking the driver.</summary>
    string Device { get; }

    /// <summary>The driver's version once it has answered, e.g. "610.62".</summary>
    string? DriverVersion { get; }

    /// <summary>The driver answered that it has no clock offsets for this GPU.</summary>
    bool Unsupported { get; }

    /// <summary>The offsets in force and the limits; null when the driver didn't answer.</summary>
    GpuClockReading? Read();

    /// <summary>Sets one clock's offset; false when the driver refused it.</summary>
    bool Write(GpuClock clock, int offsetMhz);
}
