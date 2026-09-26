using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Ipc;
using static OpenSense.Core.Hardware.OperatingMode;

namespace OpenSense.Core.Tests;

public class OperatingModePolicyTests
{
    private static readonly OperatingMode[] Predator = [Eco, Quiet, Balanced, Performance, Turbo];
    private static readonly OperatingMode[] Nitro = [Quiet, Balanced, Performance];

    [Fact]
    public void The_power_supply_limits_the_modes()
    {
        Assert.Equal(PowerLimit.None, OperatingModePolicy.Limit(onAc: true, batteryBoost: true));
        Assert.Equal(PowerLimit.None, OperatingModePolicy.Limit(onAc: true, batteryBoost: null));
        Assert.Equal(PowerLimit.LowBattery, OperatingModePolicy.Limit(onAc: true, batteryBoost: false));
        Assert.Equal(PowerLimit.Battery, OperatingModePolicy.Limit(onAc: false, batteryBoost: false));

        Assert.Equal(Predator, OperatingModePolicy.Allowed(Predator, PowerLimit.None));
        Assert.Equal([Eco, Quiet, Balanced], OperatingModePolicy.Allowed(Predator, PowerLimit.Battery));
        Assert.Equal([Quiet, Balanced], OperatingModePolicy.Allowed(Predator, PowerLimit.LowBattery));
    }

    [Theory]
    [InlineData(Turbo, null, PowerLimit.None, Turbo)]
    [InlineData(Turbo, null, PowerLimit.Battery, Balanced)] // performance modes need the adapter
    [InlineData(Quiet, null, PowerLimit.Battery, Quiet)] // as NitroSense: Quiet stays
    [InlineData(Turbo, Eco, PowerLimit.Battery, Eco)] // the battery mode, where one is chosen
    [InlineData(Turbo, Eco, PowerLimit.None, Turbo)]
    [InlineData(Performance, null, PowerLimit.LowBattery, Balanced)]
    [InlineData(Quiet, null, PowerLimit.LowBattery, Quiet)]
    [InlineData(Eco, null, PowerLimit.LowBattery, Balanced)]
    public void The_target_is_the_mode_chosen_for_the_supply_or_balanced(OperatingMode ac, OperatingMode? battery, PowerLimit limit, OperatingMode expected) =>
        Assert.Equal(expected, OperatingModePolicy.Target(new ControlProfile { OperatingMode = ac, BatteryOperatingMode = battery }, Predator, limit));

    [Fact]
    public void Without_a_chosen_mode_the_firmware_is_left_alone() =>
        Assert.Null(OperatingModePolicy.Target(new ControlProfile(), Predator, PowerLimit.Battery));

    [Theory]
    [InlineData(Balanced, Performance)]
    [InlineData(Performance, Turbo)]
    [InlineData(Turbo, Quiet)]
    [InlineData(Quiet, Balanced)]
    [InlineData(Eco, Balanced)] // outside the cycle: back to its start
    public void On_ac_the_key_cycles_like_acers(OperatingMode current, OperatingMode next) =>
        Assert.Equal(next, OperatingModePolicy.NextForKey(new ControlProfile(), current, Predator, PowerLimit.None));

    [Fact]
    public void The_cycle_skips_modes_the_laptop_lacks() =>
        Assert.Equal(Quiet, OperatingModePolicy.NextForKey(new ControlProfile(), Performance, Nitro, PowerLimit.None));

    [Theory]
    [InlineData(Balanced, Eco)]
    [InlineData(Eco, Balanced)]
    [InlineData(Quiet, Balanced)]
    public void On_battery_the_key_goes_between_balanced_and_eco(OperatingMode current, OperatingMode next) =>
        Assert.Equal(next, OperatingModePolicy.NextForKey(new ControlProfile(), current, Predator, PowerLimit.Battery));

    [Fact]
    public void On_battery_without_eco_the_key_goes_between_balanced_and_quiet()
    {
        Assert.Equal(Quiet, OperatingModePolicy.NextForKey(new ControlProfile(), Balanced, Nitro, PowerLimit.Battery));
        Assert.Equal(Balanced, OperatingModePolicy.NextForKey(new ControlProfile(), Quiet, Nitro, PowerLimit.Battery));
    }

    [Fact]
    public void With_the_battery_low_the_key_goes_between_balanced_and_quiet()
    {
        Assert.Equal(Quiet, OperatingModePolicy.NextForKey(new ControlProfile(), Balanced, Predator, PowerLimit.LowBattery));
        Assert.Equal(Balanced, OperatingModePolicy.NextForKey(new ControlProfile(), Quiet, Predator, PowerLimit.LowBattery));
    }

    [Fact]
    public void Turbo_toggle_goes_to_turbo_and_back_to_the_mode_before()
    {
        var profile = new ControlProfile { ModeKey = ModeKeyAction.TurboToggle, OperatingMode = Quiet };

        Assert.Equal(Turbo, OperatingModePolicy.NextForKey(profile, Quiet, Predator, PowerLimit.None));
        var inTurbo = OperatingModePolicy.WithKeyChoice(profile, Quiet, Turbo, PowerLimit.None);
        Assert.Equal(Turbo, inTurbo.OperatingMode);
        Assert.Equal(Quiet, inTurbo.TurboReturnMode);

        Assert.Equal(Quiet, OperatingModePolicy.NextForKey(inTurbo, Turbo, Predator, PowerLimit.None));
        Assert.Equal(Balanced, OperatingModePolicy.NextForKey(inTurbo with { TurboReturnMode = null }, Turbo, Predator, PowerLimit.None));
    }

    [Theory]
    [InlineData(PowerLimit.Battery)]
    [InlineData(PowerLimit.LowBattery)]
    public void Turbo_toggle_is_blocked_without_the_adapter_or_a_charged_battery(PowerLimit limit)
    {
        var profile = new ControlProfile { ModeKey = ModeKeyAction.TurboToggle };

        Assert.True(OperatingModePolicy.TurboBlocked(profile, limit));
        Assert.Null(OperatingModePolicy.NextForKey(profile, Balanced, Predator, limit));
    }

    [Fact]
    public void The_key_changes_the_battery_mode_on_battery_and_the_ac_mode_otherwise()
    {
        var profile = new ControlProfile { OperatingMode = Turbo };

        var onBattery = OperatingModePolicy.WithKeyChoice(profile, Balanced, Eco, PowerLimit.Battery);
        Assert.Equal(Eco, onBattery.BatteryOperatingMode);
        Assert.Equal(Turbo, onBattery.OperatingMode);

        var onAc = OperatingModePolicy.WithKeyChoice(profile, Turbo, Quiet, PowerLimit.None);
        Assert.Equal(Quiet, onAc.OperatingMode);
        Assert.Null(onAc.BatteryOperatingMode);
    }
}

public class PowerLimitControlTests
{
    private static (FanControlService Service, FakeFirmware Firmware, FakePower Power, List<ControlNotice> Notices) Create(
        ControlProfile profile, ulong modeMask = 0x33)
    {
        var firmware = new FakeFirmware { SupportsModes = true, ModeMask = modeMask };
        var device = new AcerDevice(firmware);
        var power = new FakePower();
        var service = new FanControlService(device, CapabilityProbe.Probe(device), new FakeLoad(), power, profile) { Interval = TimeSpan.Zero };
        var notices = new List<ControlNotice>();
        service.Notice += notices.Add;
        return (service, firmware, power, notices);
    }

    [Fact]
    public void A_low_battery_on_ac_holds_performance_modes_back_until_it_charges()
    {
        var (service, fw, _, _) = Create(new ControlProfile { OperatingMode = Turbo });
        fw.BatteryBoost = false;

        service.Tick();
        Assert.Equal(Balanced, fw.Mode);
        Assert.Equal(PowerLimit.LowBattery, service.Latest!.PowerLimit);

        // The firmware says the battery can help again (its event 9, whose own byte reads 0 on the AN515-57).
        fw.BatteryBoost = true;
        service.OnBatteryBoostEvent();
        service.Tick();
        Assert.Equal(Turbo, fw.Mode);
        Assert.Equal(PowerLimit.None, service.Latest.PowerLimit);
    }

    [Fact]
    public void On_battery_the_battery_mode_runs_and_unplugging_says_so()
    {
        var (service, fw, power, notices) = Create(new ControlProfile { OperatingMode = Turbo, BatteryOperatingMode = Quiet });
        service.Tick();
        Assert.Equal(Turbo, fw.Mode);

        power.IsOnAcPower = false;
        service.Tick();

        Assert.Equal(Quiet, fw.Mode);
        var notice = Assert.Single(notices, n => n.Kind == NoticeKind.OperatingModeChangedByPower);
        Assert.Equal(Quiet, notice.OperatingMode);
        Assert.Equal(PowerLimit.Battery, notice.PowerLimit);
    }

    [Fact]
    public void A_change_the_user_makes_is_not_reported_as_one_by_the_power_supply()
    {
        var (service, _, _, notices) = Create(new ControlProfile { OperatingMode = Turbo });
        service.Tick();

        service.Update(new ControlProfile { OperatingMode = Quiet });
        service.Tick();

        Assert.DoesNotContain(notices, n => n.Kind == NoticeKind.OperatingModeChangedByPower);
    }

    [Fact]
    public void Eco_keeps_the_fans_on_auto_as_acers_software_does()
    {
        var (service, fw, _, _) = Create(new ControlProfile { OperatingMode = Eco, Mode = FanControlMode.Max }, modeMask: 0x73);

        service.Tick();

        Assert.Equal(Eco, fw.Mode);
        Assert.Equal(FanBehavior.Auto, fw.Behavior[0]);
        Assert.Equal(FanLock.EcoMode, service.Latest!.FanLock);
    }

    [Fact]
    public async Task The_key_press_is_worked_out_on_the_control_thread_from_the_mode_in_force()
    {
        var (service, _, power, _) = Create(new ControlProfile { OperatingMode = Balanced });
        service.Tick();

        var onAc = service.PressModeKeyAsync(service.Profile);
        service.Tick();
        Assert.Equal(new ModeKeyPress(Balanced, Performance, PowerLimit.None), await onAc);

        power.IsOnAcPower = false;
        service.Tick();
        var onBattery = service.PressModeKeyAsync(service.Profile);
        service.Tick();
        Assert.Equal(new ModeKeyPress(Balanced, Quiet, PowerLimit.Battery), await onBattery); // no Eco on this laptop
    }
}

/// <summary>The Mode key end to end: firmware event, engine, settings, notice, capability, over the real JSON-RPC stack.</summary>
public sealed class ModeKeyIpcTests : IAsyncLifetime
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
    public async Task The_mode_key_switches_the_mode_says_so_and_shows_it_has_a_key()
    {
        var ct = TestContext.Current.CancellationToken;
        var before = await _service.GetSnapshotAsync(ct);
        Assert.False(before.Capabilities.ModeKey);
        await _service.SetProfileAsync(before.Settings.Profile with { OperatingMode = Balanced }, ct);
        var balanced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.OperatingMode == Balanced)
                balanced.TrySetResult();
        };
        await balanced.Task.WaitAsync(Timeout, ct);

        var switched = new TaskCompletionSource<ControlNotice>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rebuilt = new TaskCompletionSource<EngineSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.NoticeRaised += (_, n) =>
        {
            if (n.Kind == NoticeKind.OperatingModeSwitchedByKey)
                switched.TrySetResult(n);
        };
        _service.Rebuilt += (_, s) => rebuilt.TrySetResult(s);

        _machine.Events.Raise(7, 4, 0, 0);

        Assert.Equal(Performance, (await switched.Task.WaitAsync(Timeout, ct)).OperatingMode);
        Assert.True((await rebuilt.Task.WaitAsync(Timeout, ct)).Capabilities.ModeKey);
        var after = await _service.GetSnapshotAsync(ct);
        Assert.Equal(Performance, after.Settings.Profile.OperatingMode);
        Assert.True(after.Capabilities.ModeKey);
        Assert.Contains("\"ModeKeySeen\": true", await File.ReadAllTextAsync(Path.Combine(_directory, "state.json"), ct), StringComparison.Ordinal);
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
