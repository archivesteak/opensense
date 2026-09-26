using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Tests;

/// <summary>The GPU driver's clock offsets: counts every call, since asking a sleeping GPU wakes it.</summary>
internal sealed class FakeGpuClocks : IGpuClockControl
{
    public static readonly GpuClockLimits AcerLimits = new(-1000, 1000, -2000, 6000); // AN515-57, RTX 3070

    public ClockOffsets Offsets { get; set; } = ClockOffsets.None;
    public GpuClockLimits Limits { get; set; } = AcerLimits;
    public bool NoOffsets { get; set; }
    public bool Refuse { get; set; }
    public int Reads { get; private set; }
    public List<(GpuClock Clock, int Mhz)> Writes { get; } = [];

    public string Device => @"PCI\VEN_10DE&DEV_249D&SUBSYS_153A1025&REV_A1";
    public string? DriverVersion => "610.62";
    public bool Unsupported { get; private set; }

    public GpuClockReading? Read()
    {
        Reads++;
        Unsupported = NoOffsets;
        return NoOffsets ? null : new GpuClockReading(Offsets, Limits);
    }

    public bool Write(GpuClock clock, int offsetMhz)
    {
        Writes.Add((clock, offsetMhz));
        if (Refuse)
            return false;
        Offsets = clock == GpuClock.Core ? Offsets with { CoreMhz = offsetMhz } : Offsets with { MemoryMhz = offsetMhz };
        return true;
    }
}

public sealed class GpuClockControllerTests
{
    private static readonly ClockOffsets Overclock = new(150, 500);

    private readonly FakeGpuClocks _driver = new();

    [Fact]
    public void Offsets_are_read_before_they_are_written_and_kept_within_the_drivers_limits()
    {
        var controller = new GpuClockController(_driver, null);

        var telemetry = controller.Step(new ClockOffsets(1500, 300), gpuOn: true, reread: false);

        Assert.Equal(1, _driver.Reads);
        Assert.Equal([(GpuClock.Core, 1000), (GpuClock.Memory, 300)], _driver.Writes);
        Assert.Equal(new GpuClockTelemetry(FakeGpuClocks.AcerLimits, new ClockOffsets(1000, 300), new ClockOffsets(1000, 300)), telemetry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)] // Windows can't tell: it may be asleep
    public void A_gpu_that_may_be_off_is_never_asked(bool? gpuOn)
    {
        var controller = new GpuClockController(_driver, new GpuClockRecord(_driver.Device, "610.62", FakeGpuClocks.AcerLimits));

        var telemetry = controller.Step(Overclock, gpuOn, reread: false);

        Assert.Equal(0, _driver.Reads);
        Assert.Empty(_driver.Writes);
        // What it will set, from the limits known from last time.
        Assert.Equal(new GpuClockTelemetry(FakeGpuClocks.AcerLimits, null, Overclock), telemetry);
    }

    [Fact]
    public void Nothing_set_leaves_another_programs_offsets_alone()
    {
        _driver.Offsets = new ClockOffsets(100, 800); // e.g. MSI Afterburner's
        var controller = new GpuClockController(_driver, null);

        var telemetry = controller.Step(ClockOffsets.None, gpuOn: true, reread: false);
        controller.Restore();

        Assert.Empty(_driver.Writes);
        Assert.Equal(new GpuClockTelemetry(FakeGpuClocks.AcerLimits, new ClockOffsets(100, 800), null), telemetry);
    }

    [Fact]
    public void After_its_own_offsets_it_writes_zero_once_then_leaves_the_clocks_alone()
    {
        var controller = new GpuClockController(_driver, null);
        controller.Step(Overclock, gpuOn: true, reread: false);
        _driver.Writes.Clear();

        controller.Step(ClockOffsets.None, gpuOn: true, reread: false); // e.g. a mode without offsets
        Assert.Equal([(GpuClock.Core, 0), (GpuClock.Memory, 0)], _driver.Writes);

        _driver.Offsets = new ClockOffsets(60, 0); // another program's, from now on
        controller.Step(ClockOffsets.None, gpuOn: true, reread: true);
        controller.Restore();
        Assert.Equal(2, _driver.Writes.Count);
    }

    [Fact]
    public void Coming_back_on_it_reads_again_and_writes_only_what_the_driver_lost()
    {
        var controller = new GpuClockController(_driver, null);
        controller.Step(Overclock, gpuOn: true, reread: false);
        controller.Step(Overclock, gpuOn: false, reread: false);
        _driver.Writes.Clear();

        controller.Step(Overclock, gpuOn: true, reread: false); // the driver kept them
        Assert.Equal(2, _driver.Reads);
        Assert.Empty(_driver.Writes);

        controller.Step(Overclock, gpuOn: false, reread: false);
        _driver.Offsets = ClockOffsets.None; // lost this time
        controller.Step(Overclock, gpuOn: true, reread: false);
        Assert.Equal([(GpuClock.Core, 150), (GpuClock.Memory, 500)], _driver.Writes);
    }

    [Fact]
    public void While_on_the_driver_is_read_once_until_asked_to_read_again()
    {
        var controller = new GpuClockController(_driver, null);
        for (var i = 0; i < 5; i++)
            controller.Step(Overclock, gpuOn: true, reread: false);
        Assert.Equal(1, _driver.Reads);

        _driver.Offsets = new ClockOffsets(0, 500); // e.g. Acer's agent after a resume
        controller.Step(Overclock, gpuOn: true, reread: true);
        Assert.Equal(2, _driver.Reads);
        Assert.Equal(new ClockOffsets(150, 500), _driver.Offsets);
    }

    [Fact]
    public void Stopping_puts_its_own_offsets_back_to_zero_even_with_the_gpu_off()
    {
        var controller = new GpuClockController(_driver, null);
        controller.Step(Overclock, gpuOn: true, reread: false);
        controller.Step(Overclock, gpuOn: false, reread: false);
        _driver.Writes.Clear();

        controller.Restore();

        Assert.Equal([(GpuClock.Core, 0), (GpuClock.Memory, 0)], _driver.Writes);
    }

    [Fact]
    public void A_refusal_is_reported_once_and_not_retried_until_the_offsets_change()
    {
        _driver.Refuse = true;
        var controller = new GpuClockController(_driver, null);
        var refusals = 0;
        controller.Refused += () => refusals++;

        for (var i = 0; i < 3; i++)
            controller.Step(Overclock, gpuOn: true, reread: false);
        Assert.Equal(1, refusals);
        Assert.Equal(2, _driver.Writes.Count);

        controller.Step(new ClockOffsets(90, 500), gpuOn: true, reread: false);
        Assert.Equal(2, refusals);
    }

    [Fact]
    public void A_driver_without_offsets_is_reported_and_still_asked_after_each_power_up()
    {
        _driver.NoOffsets = true;
        var controller = new GpuClockController(_driver, null);
        var reported = new List<GpuClockLimits?>();
        controller.LimitsChanged += reported.Add;

        Assert.Null(controller.Step(Overclock, gpuOn: true, reread: false));
        Assert.Equal([null], reported);

        // A new driver may have them.
        _driver.NoOffsets = false;
        controller.Step(Overclock, gpuOn: false, reread: false);
        Assert.NotNull(controller.Step(Overclock, gpuOn: true, reread: false));
        Assert.Equal([null, FakeGpuClocks.AcerLimits], reported);
    }

    [Fact]
    public void A_driver_remembered_without_offsets_shows_none_until_it_says_otherwise()
    {
        var controller = new GpuClockController(_driver, new GpuClockRecord(_driver.Device, "560.00", null));

        Assert.Null(controller.Step(Overclock, gpuOn: false, reread: false));
        Assert.NotNull(controller.Step(Overclock, gpuOn: true, reread: false));
    }
}

public sealed class GpuClockControlLoopTests
{
    [Fact]
    public void The_offsets_follow_the_operating_mode_in_force_and_Balanceds_on_battery()
    {
        var driver = new FakeGpuClocks();
        var gpu = new FakeGpuSensor();
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = true }, SensorStatus.NotUsed, gpu.Status, driver);
        var firmware = new FakeFirmware { SupportsModes = true };
        var device = new AcerDevice(firmware);
        var power = new FakePower();
        var profile = new ControlProfile
        {
            OperatingMode = OperatingMode.Turbo,
            GpuClocks = new GpuClockSettings
            {
                Modes = new Dictionary<OperatingMode, ClockOffsets> { [OperatingMode.Turbo] = new(150, 500) },
                Fixed = new ClockOffsets(45, 0), // for laptops without modes: not used here
            },
        };
        var service = new FanControlService(device, CapabilityProbe.Probe(device), new FakeLoad(), power, profile, sensors);

        service.Tick();
        Assert.Equal(new ClockOffsets(150, 500), driver.Offsets);
        Assert.Equal(new ClockOffsets(150, 500), service.Latest!.GpuClocks!.Applied);

        power.IsOnAcPower = false; // Turbo falls back to Balanced, which has none: OpenSense's own go back to 0
        service.Tick();
        Assert.Equal(OperatingMode.Balanced, service.Latest.OperatingMode);
        Assert.Equal(ClockOffsets.None, driver.Offsets);
        Assert.Null(service.Latest.GpuClocks!.Target);
    }

    [Fact]
    public void Without_operating_modes_the_fixed_offsets_apply()
    {
        var driver = new FakeGpuClocks();
        var gpu = new FakeGpuSensor();
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = true }, SensorStatus.NotUsed, gpu.Status, driver);
        var device = new AcerDevice(new FakeFirmware());
        var profile = new ControlProfile { GpuClocks = new GpuClockSettings { Fixed = new ClockOffsets(45, 200) } };
        var service = new FanControlService(device, CapabilityProbe.Probe(device), new FakeLoad(), new FakePower(), profile, sensors);

        service.Tick();

        Assert.Equal(new ClockOffsets(45, 200), driver.Offsets);
    }

    [Fact]
    public void A_gpu_asleep_is_not_woken_for_its_clocks()
    {
        var driver = new FakeGpuClocks();
        var gpu = new FakeGpuSensor();
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = false }, SensorStatus.NotUsed, gpu.Status, driver);
        var device = new AcerDevice(new FakeFirmware());
        var profile = new ControlProfile { GpuClocks = new GpuClockSettings { Fixed = new ClockOffsets(45, 200) } };
        var service = new FanControlService(device, CapabilityProbe.Probe(device), new FakeLoad(), new FakePower(), profile, sensors);

        service.Tick();

        Assert.Equal(0, driver.Reads);
        Assert.Null(service.Latest!.GpuClocks!.Applied);
    }
}
