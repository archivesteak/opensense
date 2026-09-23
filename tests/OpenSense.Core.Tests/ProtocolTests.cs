using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

public class ProtocolTests
{
    [Theory]
    [InlineData(FanBehavior.Auto, 0x410009UL)]
    [InlineData(FanBehavior.Max, 0x820009UL)]
    [InlineData(FanBehavior.Custom, 0xC30009UL)]
    public void Both_fans_behaviour_matches_documented_encoding(FanBehavior behavior, ulong expected)
    {
        var input = AcerProtocol.FanBehaviorInput([(FanChannel.Cpu, behavior), (FanChannel.Gpu, behavior)]);
        Assert.Equal(expected, input);
    }

    [Fact]
    public void Single_fan_behaviour_only_sets_its_own_bits()
    {
        Assert.Equal(0x1UL | (3UL << 16), AcerProtocol.FanBehaviorInput([(FanChannel.Cpu, FanBehavior.Custom)]));
        Assert.Equal(0x8UL | (3UL << 22), AcerProtocol.FanBehaviorInput([(FanChannel.Gpu, FanBehavior.Custom)]));
    }

    [Theory]
    [InlineData(55, 0x3701UL)]
    [InlineData(150, 0x6401UL)]
    [InlineData(-5, 0x0001UL)]
    public void Fan_speed_is_clamped_and_packed(int percent, ulong expected) =>
        Assert.Equal(expected, AcerProtocol.FanSpeedInput(FanChannel.Cpu, percent));

    [Fact]
    public void Gpu_fan_speed_uses_id_4() =>
        Assert.Equal(0x3204UL, AcerProtocol.FanSpeedInput(FanChannel.Gpu, 50));

    [Fact]
    public void Sensor_read_encoding()
    {
        Assert.Equal(0x0101u, AcerProtocol.SensorReadInput(SensorId.CpuTemperature));
        Assert.Equal(0x0A01u, AcerProtocol.SensorReadInput(SensorId.GpuTemperature));
        Assert.Equal(3150, AcerProtocol.SensorValue(0x0C4E00UL));
        Assert.True(AcerProtocol.IsOk(0x0C4E00UL));
        Assert.False(AcerProtocol.IsOk(0x0C4E01UL));
    }

    [Fact]
    public void CoolBoost_and_misc_encoding()
    {
        Assert.Equal(0x10007UL, AcerProtocol.CoolBoostSetInput(true));
        Assert.Equal(0x7UL, AcerProtocol.CoolBoostSetInput(false));
        Assert.True(AcerProtocol.CoolBoostValue(0x100));
        Assert.Equal(0x040BUL, AcerProtocol.MiscSetInput(MiscSetting.OperatingMode, (byte)OperatingMode.Performance));
        Assert.Equal(0x0202UL, AcerProtocol.MiscSetInput(MiscSetting.GpuMode, (byte)GpuMode.Discrete));
    }

    [Fact]
    public void Operating_mode_mask_decodes_by_bit_position()
    {
        var output = ((1UL << 0) | (1UL << 1) | (1UL << 4)) << 8;
        Assert.Equal([OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance], AcerProtocol.DecodeOperatingModeMask(output));
    }
}
