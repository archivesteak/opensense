namespace OpenSense.Core.Hardware;

/// <summary>
/// A fan as the firmware addresses it: a bit in the behaviour mask, an id for speed commands,
/// and the sensor that reports its RPM.
/// </summary>
public sealed record FanChannel(FanId Id, string Name, int GroupBit, byte SpeedId, SensorId RpmSensor)
{
    public static readonly FanChannel Cpu = new(FanId.Cpu, "CPU", GroupBit: 0, SpeedId: 1, SensorId.CpuFanSpeed);
    public static readonly FanChannel Gpu = new(FanId.Gpu, "GPU", GroupBit: 3, SpeedId: 4, SensorId.GpuFanSpeed);

    public static IReadOnlyList<FanChannel> Known { get; } = [Cpu, Gpu];

    public static FanChannel Get(FanId id) => Known.First(f => f.Id == id);
}
