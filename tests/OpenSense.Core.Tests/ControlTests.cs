using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Tests;

public class CurveTests
{
    [Theory]
    [InlineData(30, 0)]
    [InlineData(70, 0)]
    [InlineData(75, 10)]
    [InlineData(85, 35)]
    [InlineData(100, 100)]
    public void Default_curve_interpolates_and_clamps(double temperature, int expected) =>
        Assert.Equal(expected, CurvePresets.Default.Evaluate(temperature));

    [Fact]
    public void Points_are_sorted_and_clamped()
    {
        var curve = FanCurve.From((90, 150), (10, -5), (50, 40));
        Assert.Equal([new CurvePoint(20, 0), new CurvePoint(50, 40), new CurvePoint(90, 100)], curve.Points);
    }

    [Fact]
    public void Follower_rises_at_once_and_falls_one_reading_later()
    {
        var curve = FanCurve.From((40, 0), (80, 80)); // 2 % per °C
        var f = new CurveFollower();

        Assert.Equal(70, f.Update(75, curve));
        Assert.Equal(70, f.Update(69, curve)); // a drop alone holds 75's boost
        Assert.Equal(58, f.Update(68, curve)); // the next reading confirms it: 69's boost
        Assert.Equal(56, f.Update(68, curve));
        Assert.Equal(70, f.Update(75, curve)); // rising: at once
    }

    [Fact]
    public void Follower_holds_through_a_wobble_and_ignores_tiny_changes()
    {
        var curve = FanCurve.From((40, 0), (80, 80));
        var f = new CurveFollower();

        foreach (var temperature in new[] { 60.0, 58, 60, 58, 60 })
            Assert.Equal(40, f.Update(temperature, curve));
        Assert.Equal(40, f.Update(60.4, curve)); // 41 %: under the 2 % step
    }
}

public class FanControlServiceTests
{
    private static (FanControlService Service, FakeFirmware Firmware, FakePower Power) Create(ControlProfile profile, bool modes = false,
        Func<DateTime>? clock = null, DirectSensors? sensors = null)
    {
        var firmware = new FakeFirmware { SupportsModes = modes };
        var device = new AcerDevice(firmware);
        var caps = CapabilityProbe.Probe(device);
        var power = new FakePower();
        var service = new FanControlService(device, caps, new FakeLoad(), power, profile, sensors, clock);
        firmware.Calls.Clear();
        return (service, firmware, power);
    }

    [Fact]
    public void Custom_mode_sets_behaviour_then_boosts_and_leaves_a_zero_boost_on_auto()
    {
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Custom,
            Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(70), [FanId.Gpu] = new(0) },
        };
        var (service, fw, _) = Create(profile);

        service.Tick();

        Assert.Equal(FanBehavior.Custom, fw.Behavior[0]);
        Assert.Equal(FanBehavior.Auto, fw.Behavior[3]);
        Assert.Equal(70, fw.Speed[1]);
        Assert.False(fw.Speed.ContainsKey(4));
        Assert.Equal(70, service.Latest!.Fan(FanId.Cpu)!.BoostPercent);
        Assert.Null(service.Latest.Fan(FanId.Gpu)!.BoostPercent);
    }

    [Fact]
    public void Unchanged_state_is_not_resent()
    {
        var (service, fw, _) = Create(new ControlProfile { Mode = FanControlMode.Max });
        service.Tick();
        var writes = fw.Writes.Count();
        service.Tick();
        Assert.Equal(writes, fw.Writes.Count());
    }

    [Fact]
    public void Missing_cpu_temperature_hands_fans_back_to_auto()
    {
        var (service, fw, _) = Create(new ControlProfile { Mode = FanControlMode.Custom });
        service.Tick();
        Assert.Equal(FanBehavior.Custom, fw.Behavior[0]);

        fw.FailCpuTemp = true;
        for (var i = 0; i < 3; i++)
        {
            SkipSampleThrottle(service);
            service.Tick();
        }
        Assert.Equal(FanBehavior.Auto, fw.Behavior[0]);
        Assert.True(service.Latest!.Failsafe);
    }

    [Fact]
    public void Quiet_mode_locks_fans_to_auto()
    {
        var (service, fw, _) = Create(new ControlProfile { Mode = FanControlMode.Max, OperatingMode = OperatingMode.Quiet }, modes: true);
        service.Tick();
        Assert.Equal(OperatingMode.Quiet, fw.Mode);
        Assert.Equal(FanBehavior.Auto, fw.Behavior[0]);
        Assert.Equal(FanLock.QuietMode, service.Latest!.FanLock);
    }

    [Fact]
    public void Performance_mode_falls_back_to_balanced_on_battery()
    {
        var (service, fw, power) = Create(new ControlProfile { OperatingMode = OperatingMode.Performance }, modes: true);
        power.IsOnAcPower = false;
        service.Tick();
        Assert.Equal(OperatingMode.Balanced, fw.Mode);

        power.IsOnAcPower = true;
        service.Tick();
        Assert.Equal(OperatingMode.Performance, fw.Mode);
    }

    [Fact]
    public void Custom_curves_follow_temperature()
    {
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Custom,
            Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(UseCurve: true), [FanId.Gpu] = new(UseCurve: true) },
            Curves = new Dictionary<FanId, CurveFanSetting>
            {
                [FanId.Cpu] = new(FanCurve.From((40, 0), (80, 80))),
                [FanId.Gpu] = new(FanCurve.From((40, 0), (80, 80))),
            },
        };
        var (service, fw, _) = Create(profile);
        fw.CpuTemp = 60;
        fw.GpuTemp = 50;
        service.Tick();
        Assert.Equal(40, fw.Speed[1]);
        Assert.Equal(20, fw.Speed[4]);
    }

    [Fact]
    public void Auto_boosts_only_when_hot()
    {
        var now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        var (service, fw, _) = Create(new ControlProfile(), clock: () => now);

        fw.CpuTemp = 60;
        fw.GpuTemp = 50;
        service.Tick();
        Assert.Equal(FanBehavior.Auto, fw.Behavior[0]);
        Assert.Equal(FanBehavior.Auto, fw.Behavior[3]);
        Assert.Empty(fw.Speed);

        fw.CpuTemp = 90; // Default: 50 % at 90 °C
        now += FanControlService.CurveInterval;
        service.Tick();
        Assert.Equal(FanBehavior.Custom, fw.Behavior[0]);
        Assert.Equal(50, fw.Speed[1]);
        Assert.Equal(FanBehavior.Auto, fw.Behavior[3]); // the GPU fan follows the GPU, still cool
    }

    [Fact]
    public void A_sleeping_gpu_gets_no_boost()
    {
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Custom,
            Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(0), [FanId.Gpu] = new(UseCurve: true) },
            Curves = new Dictionary<FanId, CurveFanSetting> { [FanId.Gpu] = new(FanCurve.From((20, 60), (100, 60))) },
        };
        var (service, fw, _) = Create(profile);
        fw.GpuTemp = 0; // the firmware's answer while the discrete GPU is powered down
        service.Tick();
        Assert.Equal(FanBehavior.Auto, fw.Behavior[3]);
        Assert.True(service.Latest!.GpuAsleep);
    }

    [Fact]
    public void A_gpu_windows_reports_on_is_read_from_the_driver_though_the_firmware_reads_zero()
    {
        // After a resume from sleep the firmware can go on reading 0 with the GPU back on.
        var gpu = new FakeGpuSensor { Temperature = 62 };
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = true }, SensorStatus.NotUsed, gpu.Status);
        var (service, fw, _) = Create(new ControlProfile(), sensors: sensors);
        fw.GpuTemp = 0;

        service.Tick();

        Assert.False(service.Latest!.GpuAsleep);
        Assert.Equal(62d, service.Latest.GpuTemperature);
        Assert.Equal(TemperatureOrigin.GpuDriver, service.Latest.GpuTemperatureOrigin);
    }

    [Theory]
    [InlineData(false, 55)] // Windows says off: the driver isn't asked, even with a temperature from the firmware
    [InlineData(false, 0)]
    [InlineData(null, 0)] // Windows can't tell: the firmware's 0 says off
    public void A_gpu_that_is_off_is_not_woken_by_asking_the_driver(bool? windowsSaysOn, int firmwareTemp)
    {
        var gpu = new FakeGpuSensor();
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = windowsSaysOn }, SensorStatus.NotUsed, gpu.Status);
        var (service, fw, _) = Create(new ControlProfile(), sensors: sensors);
        fw.GpuTemp = firmwareTemp;

        service.Tick();

        Assert.Equal(0, gpu.Reads);
        Assert.Equal(firmwareTemp == 0, service.Latest!.GpuAsleep);
        Assert.Equal(firmwareTemp > 0 ? firmwareTemp : (double?)null, service.Latest.GpuTemperature);
    }

    [Fact]
    public void Without_a_firmware_gpu_sensor_the_gpu_fan_follows_the_drivers_reading()
    {
        var gpu = new FakeGpuSensor { Temperature = 50 };
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = true }, SensorStatus.NotUsed, gpu.Status);
        var (service, fw) = CreateWithoutFirmwareGpuTemperature(BothFansOnCurves, sensors);
        fw.CpuTemp = 60;

        service.Tick();

        Assert.Equal(50d, service.Latest!.GpuTemperature);
        Assert.Equal(TemperatureOrigin.GpuDriver, service.Latest.GpuTemperatureOrigin);
        Assert.Equal(40, fw.Speed[1]);
        Assert.Equal(20, fw.Speed[4]);
    }

    [Fact]
    public void Without_a_firmware_gpu_sensor_or_a_known_gpu_power_state_the_gpu_fan_follows_the_cpu()
    {
        var gpu = new FakeGpuSensor { Temperature = 50 };
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = null }, SensorStatus.NotUsed, gpu.Status);
        var (service, fw) = CreateWithoutFirmwareGpuTemperature(BothFansOnCurves, sensors);
        fw.CpuTemp = 60;

        service.Tick();

        Assert.Equal(0, gpu.Reads); // it may be asleep: asking could wake it
        Assert.Null(service.Latest!.GpuTemperature);
        Assert.False(service.Latest.GpuAsleep);
        Assert.Equal(40, fw.Speed[4]);
    }

    private static ControlProfile BothFansOnCurves { get; } = new()
    {
        Mode = FanControlMode.Custom,
        Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(UseCurve: true), [FanId.Gpu] = new(UseCurve: true) },
        Curves = new Dictionary<FanId, CurveFanSetting>
        {
            [FanId.Cpu] = new(FanCurve.From((40, 0), (80, 80))), // 2 % per °C
            [FanId.Gpu] = new(FanCurve.From((40, 0), (80, 80))),
        },
    };

    private static (FanControlService Service, FakeFirmware Firmware) CreateWithoutFirmwareGpuTemperature(ControlProfile profile, DirectSensors sensors)
    {
        var firmware = new FakeFirmware();
        var device = new AcerDevice(firmware);
        var probed = CapabilityProbe.Probe(device);
        var caps = probed with { Sensors = probed.Sensors.Where(s => s != SensorId.GpuTemperature).ToHashSet() };
        return (new FanControlService(device, caps, new FakeLoad(), new FakePower(), profile, sensors), firmware);
    }

    [Fact]
    public void Auto_without_its_boost_leaves_the_fans_to_the_firmware_even_when_hot()
    {
        var (service, fw, _) = Create(new ControlProfile { AutoBoost = false });
        fw.CpuTemp = 97;
        for (var i = 0; i < 3; i++)
        {
            SkipSampleThrottle(service);
            service.Tick();
        }
        Assert.Equal(FanBehavior.Auto, fw.Behavior[0]);
        Assert.Empty(fw.Speed);
    }

    [Fact]
    public void Curves_step_every_three_seconds_on_the_average_temperature()
    {
        var now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        var curve = FanCurve.From((40, 0), (80, 80)); // 2 % per °C
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Custom,
            Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(UseCurve: true), [FanId.Gpu] = new(0) },
            Curves = new Dictionary<FanId, CurveFanSetting> { [FanId.Cpu] = new(curve) },
        };
        var (service, fw, _) = Create(profile, clock: () => now);

        fw.CpuTemp = 60;
        service.Tick();
        Assert.Equal(40, fw.Speed[1]);

        foreach (var temperature in new[] { 70, 74, 78 })
        {
            now += TimeSpan.FromSeconds(1);
            fw.CpuTemp = temperature;
            service.Tick();
            if (temperature != 78)
                Assert.Equal(40, fw.Speed[1]); // held between steps
        }
        Assert.Equal(68, fw.Speed[1]); // (70 + 74 + 78) / 3 = 74 °C
    }

    [Fact]
    public void Exiting_restores_auto()
    {
        var (service, fw, _) = Create(new ControlProfile { Mode = FanControlMode.Max });
        service.Start();
        SpinWait.SpinUntil(() => service.Latest is not null, TimeSpan.FromSeconds(5));
        Assert.Equal(FanBehavior.Max, fw.Behavior[0]);

        service.Dispose();
        Assert.Equal(FanBehavior.Auto, fw.Behavior[0]);
        Assert.Equal(FanBehavior.Auto, fw.Behavior[3]);
    }

    [Fact]
    public void First_run_profile_keeps_the_current_firmware_state()
    {
        var fw = new FakeFirmware();
        fw.Invoke(AcerProtocol.GamingClass, "SetGamingFanBehavior", 0x1UL | (3UL << 16)); // CPU custom, GPU untouched
        fw.Invoke(AcerProtocol.GamingClass, "SetGamingFanSpeed", 0x6401); // CPU 100 %
        var device = new AcerDevice(fw);
        var caps = CapabilityProbe.Probe(device);

        var profile = FirmwareState.Read(device, caps).ToProfile(new ControlProfile());

        Assert.Equal(FanControlMode.Custom, profile.Mode);
        Assert.Equal(new ManualFanSetting(100), profile.ManualFor(FanId.Cpu));
        Assert.Equal(new ManualFanSetting(0), profile.ManualFor(FanId.Gpu)); // left on Auto = no boost
    }

    /// <summary>Tick() only re-reads sensors once per interval; tests move faster than that.</summary>
    private static void SkipSampleThrottle(FanControlService service) => service.Interval = TimeSpan.Zero;
}
