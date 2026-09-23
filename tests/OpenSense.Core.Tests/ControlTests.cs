using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

public class CurveTests
{
    [Theory]
    [InlineData(30, 15)]
    [InlineData(40, 15)]
    [InlineData(45, 20)]
    [InlineData(75, 65)]
    [InlineData(99, 100)]
    public void Balanced_curve_interpolates_and_clamps(double temperature, int expected) =>
        Assert.Equal(expected, CurvePresets.Balanced.Evaluate(temperature));

    [Fact]
    public void Points_are_sorted_and_clamped()
    {
        var curve = FanCurve.From((90, 150), (10, -5), (50, 40));
        Assert.Equal([new CurvePoint(20, 0), new CurvePoint(50, 40), new CurvePoint(90, 100)], curve.Points);
    }

    [Fact]
    public void Follower_rises_fast_but_needs_hysteresis_to_fall()
    {
        var curve = FanCurve.From((40, 0), (80, 80)); // 2 % per °C
        var response = new ResponseSettings { RiseSmoothing = 1, FallSmoothing = 1, HysteresisC = 3, MinChangePercent = 1 };
        var f = new CurveFollower();

        Assert.Equal(40, f.Update(60, curve, response, 0));
        Assert.Equal(40, f.Update(58, curve, response, 0)); // 58+3=61 → 42 ≥ 40: hold
        Assert.Equal(40, f.Update(57, curve, response, 0)); // 57+3=60 → 40: hold
        Assert.Equal(38, f.Update(56, curve, response, 0)); // 56+3=59 → 38: drop
        Assert.Equal(50, f.Update(65, curve, response, 0)); // rising: immediate
    }

    [Fact]
    public void Follower_respects_minimum()
    {
        var f = new CurveFollower();
        Assert.Equal(25, f.Update(30, CurvePresets.Silent, new ResponseSettings(), minimumPercent: 25));
    }
}

public class FanControlServiceTests
{
    private static (FanControlService Service, FakeFirmware Firmware, FakePower Power) Create(ControlProfile profile, bool modes = false)
    {
        var firmware = new FakeFirmware { SupportsModes = modes };
        var device = new AcerDevice(firmware);
        var caps = CapabilityProbe.Probe(device);
        var power = new FakePower();
        var service = new FanControlService(device, caps, new FakeLoad(), power, profile);
        firmware.Calls.Clear();
        return (service, firmware, power);
    }

    [Fact]
    public void Custom_mode_sets_behaviour_then_speeds()
    {
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Custom,
            Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(false, 70), [FanId.Gpu] = new(true, 30) },
        };
        var (service, fw, _) = Create(profile);

        service.Tick();

        Assert.Equal(FanBehavior.Custom, fw.Behavior[0]);
        Assert.Equal(FanBehavior.Auto, fw.Behavior[3]);
        Assert.Equal(70, fw.Speed[1]);
        Assert.False(fw.Speed.ContainsKey(4));
        Assert.Equal(70, service.Latest!.Fan(FanId.Cpu)!.CommandedPercent);
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
    public void Emergency_temperature_forces_max_then_releases_with_margin()
    {
        var (service, fw, _) = Create(new ControlProfile { Mode = FanControlMode.Custom });
        var notices = new List<ControlNotice>();
        service.Notice += notices.Add;

        fw.CpuTemp = 96;
        service.Tick();
        Assert.NotEqual(FanBehavior.Max, fw.Behavior[0]); // one hot sample may be a turbo spike

        SkipSampleThrottle(service);
        service.Tick();
        Assert.Equal(FanBehavior.Max, fw.Behavior[0]);
        Assert.True(service.Latest!.Emergency);
        Assert.Empty(notices); // no banner: the fan tiles say "Emergency full speed"

        fw.CpuTemp = 90; // below threshold but inside the release margin
        SkipSampleThrottle(service);
        service.Tick();
        Assert.Equal(FanBehavior.Max, fw.Behavior[0]);

        fw.CpuTemp = 80;
        SkipSampleThrottle(service);
        service.Tick();
        Assert.Equal(FanBehavior.Custom, fw.Behavior[0]);
    }

    [Fact]
    public void Missing_cpu_temperature_hands_fans_back_to_auto()
    {
        var (service, fw, _) = Create(new ControlProfile { Mode = FanControlMode.Curve });
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
    public void Curve_mode_follows_temperature()
    {
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Curve,
            Curves = new Dictionary<FanId, CurveFanSetting>
            {
                [FanId.Cpu] = new(FanCurve.From((40, 0), (80, 80)), TemperatureSource.Cpu),
                [FanId.Gpu] = new(FanCurve.From((40, 0), (80, 80)), TemperatureSource.Gpu),
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
        Assert.Equal(new ManualFanSetting(false, 100), profile.ManualFor(FanId.Cpu));
        Assert.True(profile.ManualFor(FanId.Gpu).Auto);
    }

    /// <summary>Tick() only re-reads sensors once per interval; tests move faster than that.</summary>
    private static void SkipSampleThrottle(FanControlService service) => service.Interval = TimeSpan.Zero;
}
