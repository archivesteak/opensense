using System.Text.Json.Serialization;

namespace OpenSense.Core.Hardware;

/// <summary>
/// A fan as the firmware addresses it: the sensor that reports its RPM and, for fans OpenSense can drive, a bit in
/// the behaviour mask and an id for speed commands. GPU fans continue from bit 3 and speed id 4, one per fan.
/// System fans only report their speed: the firmware has no known control for them.
/// </summary>
public sealed record FanChannel(FanId Id, string Name, FanChip Chip, SensorId RpmSensor, int? GroupBit = null, byte? SpeedId = null)
{
    public static readonly FanChannel Cpu = new(FanId.Cpu, "CPU", FanChip.Cpu, SensorId.CpuFanSpeed, GroupBit: 0, SpeedId: 1);
    public static readonly FanChannel Gpu = new(FanId.Gpu, "GPU", FanChip.Gpu, SensorId.GpuFanSpeed, GroupBit: 3, SpeedId: 4);
    public static readonly FanChannel Gpu2 = new(FanId.Gpu2, "GPU 2", FanChip.Gpu, SensorId.Gpu2FanSpeed, GroupBit: 4, SpeedId: 5);
    public static readonly FanChannel System = new(FanId.System, "System", FanChip.System, SensorId.SystemFanSpeed);
    public static readonly FanChannel System2 = new(FanId.System2, "System 2", FanChip.System, SensorId.System2FanSpeed);

    public static IReadOnlyList<FanChannel> Known { get; } = [Cpu, Gpu, Gpu2, System, System2];

    /// <summary>OpenSense can set this fan's behaviour and boost.</summary>
    [JsonIgnore]
    public bool Controllable => GroupBit is not null && SpeedId is not null;

    public static FanChannel Get(FanId id) => Known.First(f => f.Id == id);
}
