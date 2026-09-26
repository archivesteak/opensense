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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 10)]
    [InlineData(14, 10)]
    [InlineData(24, 20)]
    [InlineData(25, 30)]
    [InlineData(95, 100)]
    [InlineData(100, 100)]
    [InlineData(-5, 0)]
    [InlineData(130, 100)]
    public void A_boost_goes_to_the_nearest_ten_the_firmware_does(int percent, int expected) =>
        Assert.Equal(expected, AcerProtocol.NearestFanSpeed(percent));

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

    // Made-up serials in the format of an Acer label: 10-character part number, then 123 | 0ABCD (hex) | 7 | 6 | 00.
    [Theory]
    [InlineData("NHQ7PEU00A1230ABCD7600", "12304398176")]
    [InlineData("NHQ7PEU00A1230abcd7600", "12304398176")]
    [InlineData("NHQ7PEU00A1230ABCD7B00", "123043981711")]
    [InlineData("NHQ7PEU00A1230000017600", null)]
    [InlineData("NHQ7PEU00A123XYZWV7600", null)]
    [InlineData("To be filled by O.E.M.", null)]
    [InlineData(null, null)]
    public void Snid_is_derived_from_the_serial_number(string? serial, string? snid) =>
        Assert.Equal(snid, SystemInfo.Snid(serial));
}
