using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;

namespace OpenSense.Core.Tests;

public class FanChannelTests
{
    /// <summary>The bitmap of a laptop with a second GPU fan (sensor 9) and a system fan (sensor 4), besides the AN515-57's sensors.</summary>
    internal const ulong ThreeFanSensors = (0x227UL | 0x100 | 0x8) << 24;

    [Fact]
    public void A_second_gpu_fan_continues_the_gpu_fans_group_bit_and_speed_id()
    {
        Assert.Equal(0x10UL | (3UL << 24), AcerProtocol.FanBehaviorInput([(FanChannel.Gpu2, FanBehavior.Custom)]));
        Assert.Equal(0x19UL | (1UL << 16) | (2UL << 22) | (3UL << 24),
            AcerProtocol.FanBehaviorInput([(FanChannel.Cpu, FanBehavior.Auto), (FanChannel.Gpu, FanBehavior.Max), (FanChannel.Gpu2, FanBehavior.Custom)]));
        Assert.Equal(0x3205UL, AcerProtocol.FanSpeedInput(FanChannel.Gpu2, 50));
        Assert.Equal(0x10u, AcerProtocol.FanBehaviorQuery(FanChannel.Gpu2));
        Assert.Equal(5u, AcerProtocol.FanBoostQuery(FanChannel.Gpu2));
        Assert.Equal(FanBehavior.Custom, AcerProtocol.FanBehaviorValue(3UL << 16, FanChannel.Gpu2));
    }

    [Fact]
    public void System_fans_only_report_their_speed()
    {
        Assert.True(FanChannel.Gpu2.Controllable);
        Assert.False(FanChannel.System.Controllable);
        Assert.False(FanChannel.System2.Controllable);
        Assert.Throws<ArgumentException>(() => AcerProtocol.FanBehaviorInput([(FanChannel.System, FanBehavior.Max)]));
        Assert.Throws<ArgumentException>(() => AcerProtocol.FanSpeedInput(FanChannel.System2, 50));
    }

    [Fact]
    public void Fans_are_found_from_the_sensor_bitmap()
    {
        var caps = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { SensorMask = ThreeFanSensors }));

        Assert.Equal([FanId.Cpu, FanId.Gpu, FanId.Gpu2, FanId.System], caps.Fans.Select(f => f.Id));
        Assert.Equal([FanId.Cpu, FanId.Gpu, FanId.Gpu2], caps.ControllableFans.Select(f => f.Id));
        Assert.Equal(FanChip.Gpu, caps.Fans.Single(f => f.Id == FanId.Gpu2).Chip);
    }

    [Fact]
    public void The_an515_57_has_two_fans_and_no_dust_defender()
    {
        var caps = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware()));

        Assert.Equal([FanId.Cpu, FanId.Gpu], caps.Fans.Select(f => f.Id));
        Assert.False(caps.DustDefender);
        Assert.Contains("Dust Defender raw=0x10000", caps.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void The_firmware_state_is_read_only_for_fans_it_can_drive()
    {
        var fw = new FakeFirmware { SensorMask = ThreeFanSensors };
        var device = new AcerDevice(fw);

        var state = FirmwareState.Read(device, CapabilityProbe.Probe(device));

        Assert.Equal([FanId.Cpu, FanId.Gpu, FanId.Gpu2], state.FanBehaviors.Keys.Order());
    }
}

public class ThreeFanControlTests
{
    private static (FanControlService Service, FakeFirmware Firmware) Create(ControlProfile profile, Func<DateTime>? clock = null,
        bool dustDefender = false)
    {
        var firmware = new FakeFirmware { SensorMask = FanChannelTests.ThreeFanSensors, SupportsDustDefender = dustDefender };
        var device = new AcerDevice(firmware);
        var service = new FanControlService(device, CapabilityProbe.Probe(device), new FakeLoad(), new FakePower(), profile, clock: clock);
        firmware.Calls.Clear();
        return (service, firmware);
    }

    private static ControlProfile Custom(int cpu, int gpu, int gpu2) => new()
    {
        Mode = FanControlMode.Custom,
        Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(cpu), [FanId.Gpu] = new(gpu), [FanId.Gpu2] = new(gpu2) },
    };

    [Fact]
    public void Every_gpu_fan_is_driven_and_the_system_fan_is_only_read()
    {
        var (service, fw) = Create(Custom(40, 50, 60));

        service.Tick();

        Assert.Equal(FanBehavior.Custom, fw.Behavior[4]);
        Assert.Equal(60, fw.Speed[5]);
        var behaviour = Assert.Single(fw.Writes, w => w.Method == "SetGamingFanBehavior");
        Assert.Equal(0x19UL, behaviour.Input & 0xFFFF); // CPU, GPU and GPU 2; no bit for the system fan
        Assert.Equal([FanId.Cpu, FanId.Gpu, FanId.Gpu2, FanId.System], service.Latest!.Fans.Select(f => f.Id));
        var system = service.Latest.Fan(FanId.System)!;
        Assert.Equal(2000, system.Rpm);
        Assert.Equal(FanBehavior.Auto, system.Behavior);
        Assert.Null(system.BoostPercent);
    }

    [Fact]
    public void A_second_gpu_fan_follows_the_gpu()
    {
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Custom,
            Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(0), [FanId.Gpu] = new(0), [FanId.Gpu2] = new(UseCurve: true) },
            Curves = new Dictionary<FanId, CurveFanSetting> { [FanId.Gpu2] = new(FanCurve.From((40, 0), (80, 80))) }, // 2 % per °C
        };
        var (service, fw) = Create(profile);
        fw.CpuTemp = 80;
        fw.GpuTemp = 60;

        service.Tick();

        Assert.Equal(40, fw.Speed[5]);
    }

    [Fact]
    public async Task Dust_defender_is_left_alone_while_it_runs_and_the_fans_are_set_again_after()
    {
        var now = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var (service, fw) = Create(Custom(40, 50, 60), () => now, dustDefender: true);
        service.Tick();
        Assert.False(service.Latest!.DustDefenderRunning);

        Assert.Equal(DustDefenderStart.Started, await RunNow(service, service.StartDustDefenderAsync()));
        Assert.Equal(FanLock.DustDefender, service.Latest.FanLock);
        Assert.True(service.Latest.DustDefenderRunning);

        // While it runs, nothing is written to the fans, even when OpenSense would re-assert them.
        fw.Calls.Clear();
        service.ReapplyAfter(TimeSpan.Zero);
        now += TimeSpan.FromSeconds(31);
        service.Tick();
        Assert.DoesNotContain(fw.Writes, w => w.Method.Contains("Fan", StringComparison.Ordinal));
        Assert.Equal(DustDefenderStart.Running, await RunNow(service, service.StartDustDefenderAsync()));

        // The firmware says it is done: the fans get OpenSense's settings back.
        fw.DustDefenderRunning = false;
        now += TimeSpan.FromSeconds(5);
        service.Tick();
        service.Tick();
        Assert.Null(service.Latest.FanLock);
        Assert.False(service.Latest.DustDefenderRunning);
        Assert.Contains(fw.Writes, w => w.Method == "SetGamingFanBehavior");
        Assert.Contains(fw.Writes, w => w.Method == "SetGamingFanSpeed" && w.Input == 0x3C05);
    }

    [Fact]
    public async Task A_busy_embedded_controller_turns_dust_defender_down()
    {
        var (service, fw) = Create(new ControlProfile(), dustDefender: true);
        fw.DustDefenderBusy = true;

        Assert.Equal(DustDefenderStart.Busy, await RunNow(service, service.StartDustDefenderAsync()));
        Assert.False(service.Latest!.DustDefenderRunning);
        Assert.Null(service.Latest.FanLock);
    }

    [Fact]
    public void A_run_the_firmware_starts_itself_is_followed_from_its_events()
    {
        var (service, fw) = Create(Custom(40, 50, 60), dustDefender: true);
        service.Tick();

        service.OnDustDefenderEvent(running: true);
        service.Tick();
        Assert.Equal(FanLock.DustDefender, service.Latest!.FanLock);

        service.OnDustDefenderEvent(running: false);
        service.Tick();
        service.Tick();
        Assert.Null(service.Latest.FanLock);
    }

    [Fact]
    public void A_run_going_when_the_engine_starts_is_waited_for()
    {
        var (service, fw) = Create(Custom(40, 50, 60), dustDefender: true);
        fw.DustDefenderRunning = true;

        service.Tick();

        Assert.Equal(FanLock.DustDefender, service.Latest!.FanLock);
        Assert.DoesNotContain(fw.Writes, w => w.Method.Contains("Fan", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_dust_defender_nothing_is_asked_or_started()
    {
        var (service, fw) = Create(new ControlProfile());

        Assert.Equal(DustDefenderStart.Failed, await RunNow(service, service.StartDustDefenderAsync()));
        Assert.Null(service.Latest!.DustDefenderRunning);
        Assert.DoesNotContain(fw.Calls, c => c.Input is AcerProtocol.DustDefenderQuery or AcerProtocol.DustDefenderStatusQuery);
    }

    /// <summary>Firmware work runs on the next tick (the control thread isn't started in these tests).</summary>
    private static Task<T> RunNow<T>(FanControlService service, Task<T> work)
    {
        service.Tick();
        return work;
    }
}

public class FirmwareEventDustDefenderTests
{
    [Theory]
    [InlineData(new byte[] { 6, 1, 1, 0 }, true)]
    [InlineData(new byte[] { 6, 1, 0, 0 }, false)]
    [InlineData(new byte[] { 6, 2, 1, 0 }, null)]
    [InlineData(new byte[] { 7, 1, 1, 0 }, null)]
    [InlineData(new byte[] { 6, 1 }, null)]
    public void Thermal_events_with_value_1_report_dust_defender(byte[] detail, bool? running) =>
        Assert.Equal(running, FirmwareEvent.Decode(detail)!.DustDefenderRunning);
}

/// <summary>A 2024 Predator (three fans, Dust Defender) behind the real JSON-RPC stack.</summary>
public sealed class PredatorIpcTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
    private readonly SimulatedMachine _machine = new(SimulatedModel.Predator2024);
    private OpenSenseEngine _engine = null!;
    private StreamJsonRpc.JsonRpc _server = null!;
    private StreamJsonRpc.JsonRpc _client = null!;
    private IOpenSenseService _service = null!;

    public ValueTask InitializeAsync()
    {
        _engine = new OpenSenseEngine(_machine, Path.Combine(_directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        _engine.Start();
        (_server, _client, _service) = IpcTests.Serve(_engine);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Three_fans_and_a_speed_only_system_fan_cross_the_wire()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshot = await _service.GetSnapshotAsync(ct);

        Assert.Equal([FanId.Cpu, FanId.Gpu, FanId.Gpu2, FanId.System], snapshot.Capabilities.Fans.Select(f => f.Id));
        Assert.Equal([FanId.Cpu, FanId.Gpu, FanId.Gpu2], snapshot.Capabilities.ControllableFans.Select(f => f.Id));
        Assert.True(snapshot.Capabilities.DustDefender);
        Assert.Contains(OperatingMode.Turbo, snapshot.Capabilities.OperatingModes);

        var maxSeen = new TaskCompletionSource<Telemetry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.EffectiveMode == FanControlMode.Max && t.Fans.Count == 4)
                maxSeen.TrySetResult(t);
        };
        await _service.SetProfileAsync(new ControlProfile { Mode = FanControlMode.Max }, ct);
        var telemetry = await maxSeen.Task.WaitAsync(Timeout, ct);

        Assert.Equal(FanBehavior.Max, telemetry.Fan(FanId.Gpu2)!.Behavior);
        Assert.Equal(FanBehavior.Auto, telemetry.Fan(FanId.System)!.Behavior);
        Assert.False(telemetry.DustDefenderRunning);
    }

    [Fact]
    public async Task Dust_defender_starts_over_the_wire_and_shows_in_telemetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var running = new TaskCompletionSource<Telemetry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.DustDefenderRunning == true)
                running.TrySetResult(t);
        };

        Assert.Equal(DustDefenderStart.Started, await _service.StartDustDefenderAsync(ct));
        var telemetry = await running.Task.WaitAsync(Timeout, ct);

        Assert.Equal(FanLock.DustDefender, telemetry.FanLock);
        Assert.Equal(DustDefenderStart.Running, await _service.StartDustDefenderAsync(ct));
    }

    [Fact]
    public async Task The_firmwares_dust_defender_events_reach_the_control_loop()
    {
        var ct = TestContext.Current.CancellationToken;
        var running = new TaskCompletionSource<Telemetry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.FanLock == FanLock.DustDefender)
                running.TrySetResult(t);
        };
        await _service.GetSnapshotAsync(ct);

        _machine.Events.Raise(6, 1, 1, 0);

        await running.Task.WaitAsync(Timeout, ct);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _server.Dispose();
        _engine.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }
}
