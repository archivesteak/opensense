using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Ipc;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Tests;

/// <summary>The embedded controller's HID interface (docs/PROTOCOL.md): requests and answers, byte for byte.</summary>
public sealed class EcHidProtocolTests
{
    [Fact]
    public void Requests_carry_the_header_the_command_the_function_and_the_data()
    {
        Assert.Equal(65, EcHidProtocol.StatusRequest(EcHidStatus.Version).Length);
        Assert.Equal("A000A0000005", Head(EcHidProtocol.StatusRequest(EcHidStatus.ModeCapability), 6));
        Assert.Equal("A000A0010002", Head(EcHidProtocol.ModeRequest(), 6));
        Assert.Equal("A000A001000103", Head(EcHidProtocol.SetModeRequest(3), 7));
        Assert.Equal("A000A002000201", Head(EcHidProtocol.OverclockProfileRequest(1), 7));
        Assert.Equal("A000A00A000202", Head(EcHidProtocol.BacklightTimeoutRequest(), 7));
        Assert.Equal("A000A00A000102010064001E00", Head(EcHidProtocol.SetBacklightTimeoutRequest(100, 30), 13));
        Assert.All(EcHidProtocol.SetModeRequest(3).Skip(7), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Older_firmware_answers_the_version_first_and_newer_after_the_repeated_type()
    {
        Assert.Equal(new EcHidVersion(0, 3), EcHidProtocol.DecodeVersion(Reply(0x00, 0xE0, 0x00, 0x00, 0x00, 0x03, 0xFF, 0xFF)!));
        Assert.Equal(new EcHidVersion(0, 7), EcHidProtocol.DecodeVersion(Reply(0x00, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07)!));
        Assert.Null(EcHidProtocol.DecodeVersion(Reply(0x00, 0xE0, 0x00, 0x00, 0x02, 0x00, 0x00, 0x07)!)); // not the version's answer
        Assert.Null(EcHidProtocol.DecodeVersion(Reply(0x01, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07)!)); // not supported
        Assert.Equal("0.7", new EcHidVersion(0, 7).ToString());
    }

    [Fact]
    public void From_version_0_6_status_answers_repeat_the_type_and_carry_the_value_two_bytes_later()
    {
        var older = new EcHidVersion(0, 5);
        var newer = new EcHidVersion(0, 6);
        Assert.False(older.EchoesStatusType);
        Assert.True(newer.EchoesStatusType);
        Assert.True(new EcHidVersion(1, 0).EchoesStatusType);

        Assert.Equal((ushort)5, EcHidProtocol.DecodeStatus(Reply(0x00, 0xE0, 0x00, 0x00, 0x05, 0x00)!, EcHidStatus.ModeCapability, older));
        var answer = Reply(0x00, 0xE0, 0x00, 0x00, 0x05, 0x00, 0x04, 0x00)!;
        Assert.Equal((ushort)4, EcHidProtocol.DecodeStatus(answer, EcHidStatus.ModeCapability, newer));
        Assert.Null(EcHidProtocol.DecodeStatus(answer, EcHidStatus.BatteryBoost, newer)); // answers another type
    }

    [Theory]
    [InlineData(3, "Quiet=2 Balanced=1 Performance=0")]
    [InlineData(4, "Quiet=3 Balanced=2 Performance=1 Turbo=0")]
    [InlineData(5, "Eco=4 Quiet=3 Balanced=2 Performance=1 Turbo=0")]
    [InlineData(1, "Balanced=0")]
    [InlineData(2, "Balanced=0")]
    [InlineData(0, "")]
    [InlineData(6, "")]
    public void Mode_values_count_down_along_Quick_Access_order(ushort capability, string expected)
    {
        var modes = EcHidProtocol.Modes(capability);

        Assert.Equal(expected, string.Join(" ", modes.Select(m => $"{m}={EcHidProtocol.ModeValue(modes, m)}")));
        foreach (var mode in modes)
            Assert.Equal(mode, EcHidProtocol.ModeFromValue(modes, EcHidProtocol.ModeValue(modes, mode)!.Value));
        Assert.Null(EcHidProtocol.ModeFromValue(modes, (byte)modes.Count));
    }

    [Fact]
    public void Each_mode_uses_the_overclock_profile_of_its_value_when_there_are_that_many()
    {
        var modes = EcHidProtocol.Modes(5);

        Assert.Equal((byte)0, EcHidProtocol.OverclockProfileFor(modes, OperatingMode.Turbo, 3));
        Assert.Equal((byte)1, EcHidProtocol.OverclockProfileFor(modes, OperatingMode.Performance, 3));
        Assert.Equal((byte)2, EcHidProtocol.OverclockProfileFor(modes, OperatingMode.Balanced, 3));
        Assert.Null(EcHidProtocol.OverclockProfileFor(modes, OperatingMode.Quiet, 3));
        Assert.Null(EcHidProtocol.OverclockProfileFor(modes, OperatingMode.Eco, 3));
        Assert.Null(EcHidProtocol.OverclockProfileFor(modes, OperatingMode.Turbo, 0));
    }

    [Fact]
    public void Profiles_and_the_backlight_timeout_decode_from_their_offsets()
    {
        var profile = new byte[24];
        profile[0] = 0x00;
        profile[1] = 0xE0;
        profile[2] = 0x02;
        profile[19] = 150;
        profile[21] = 0xC8;
        Assert.Equal(new ClockOffsets(150, 200), EcHidProtocol.DecodeOverclockProfile(Reply(profile)!));

        var backlight = Reply(0x00, 0xE0, 0x0A, 0x00, 0x02, 0x02, 0x01, 0x00, 0x32, 0x00, 0x1E, 0x00)!;
        Assert.Equal((50, 30), EcHidProtocol.DecodeBacklightTimeout(backlight));
        Assert.Null(EcHidProtocol.DecodeBacklightTimeout(Reply(0x00, 0xE0, 0x0A, 0x00, 0x02, 0x03)!)); // another device
    }

    [Theory]
    [InlineData((ushort)1, (ushort)0, (ushort)0, false)] // the barrel adapter
    [InlineData((ushort)4, (ushort)0, (ushort)1, false)] // a USB-C adapter strong enough
    [InlineData((ushort)4, (ushort)0, (ushort)0, true)] // a weak USB-C adapter
    [InlineData((ushort)1, (ushort)1, (ushort)1, true)] // the controller's own limit
    public void The_controller_holds_the_modes_back_for_a_weak_adapter_or_its_own_limit(ushort adapter, ushort limit, ushort usbC, bool weak) =>
        Assert.Equal(weak, OperatingModePolicy.WeakAdapter(adapter, limit, usbC));

    [Fact]
    public void Values_that_did_not_come_hold_nothing_back() =>
        Assert.False(OperatingModePolicy.WeakAdapter(4, null, null));

    private static string Head(byte[] report, int length) => Convert.ToHexString(report, 0, length);

    /// <summary>An answer report: the id, then <paramref name="data"/>.</summary>
    private static EcHidReply? Reply(params byte[] data)
    {
        var report = new byte[EcHidProtocol.ReportLength];
        report[0] = EcHidProtocol.ReportId;
        data.CopyTo(report, 1);
        return EcHidProtocol.Decode(report);
    }
}

public sealed class EcHidDeviceTests : IDisposable
{
    private readonly FakeHidBus _bus = new();
    private readonly FakeEcHid _ec = new();

    public EcHidDeviceTests() => _bus.Devices.Add(_ec);

    public void Dispose() => _ec.Dispose();

    private EcHidDevice Open() => EcHidDevice.Open(_bus, _ => { }, machineLock: false)!;

    [Fact]
    public void Only_the_embedded_controllers_interface_opens()
    {
        var other = new FakeHidBus();
        other.Devices.Add(new FakeHidDevice(new HidDeviceInfo(@"\\?\hid#kb", 0x1025, 0x174B, 1, 0x000C, 1, 0, 0, 65)));
        Assert.Null(EcHidDevice.Open(other, _ => { }, machineLock: false));
        Assert.Null(EcHidDevice.Open(new FakeHidBus(), _ => { }, machineLock: false));
        Assert.NotNull(Open());
    }

    [Fact]
    public void Answers_neither_done_nor_refused_are_asked_again_five_times_at_most()
    {
        using var hid = Open();
        _ec.Busy = 2;
        Assert.Equal(new EcHidVersion(0, 7), hid.ReadVersion());
        Assert.Equal(3, _ec.Requests);

        _ec.Busy = 5;
        Assert.Null(hid.ReadMode());
        Assert.Equal(8, _ec.Requests);
        Assert.Equal((byte)2, hid.ReadMode());
    }

    [Fact]
    public void Not_supported_is_an_answer()
    {
        using var hid = Open();
        hid.ReadVersion();
        _ec.Status.Remove(EcHidStatus.ModeLimit);
        var before = _ec.Requests;

        Assert.Null(hid.ReadStatus(EcHidStatus.ModeLimit));
        Assert.Equal(before + 1, _ec.Requests);
    }

    [Fact]
    public void A_stale_handle_is_opened_again()
    {
        using var hid = Open();
        _ec.Broken = 1;
        Assert.Equal((byte)2, hid.ReadMode());
    }

    [Fact]
    public void Without_the_version_no_status_is_read_and_the_version_is_tried_five_times()
    {
        using var hid = Open();
        Assert.Null(hid.ReadStatus(EcHidStatus.ModeCapability));

        _ec.Busy = int.MaxValue;
        Assert.Null(hid.ReadVersion());
        Assert.Equal(25, _ec.Requests);
    }

    [Fact]
    public void Older_firmware_is_read_in_its_own_layout()
    {
        _ec.Version = new EcHidVersion(0, 3);
        using var hid = Open();

        Assert.Equal(new EcHidVersion(0, 3), hid.ReadVersion());
        Assert.Equal((ushort)5, hid.ReadStatus(EcHidStatus.ModeCapability));
        Assert.Null(hid.ReadBacklightTimeout()); // not before 0.6
    }

    [Fact]
    public void Modes_profiles_and_the_backlight_timeout_are_read_and_set()
    {
        using var hid = Open();
        hid.ReadVersion();

        Assert.True(hid.WriteMode(0));
        Assert.Equal((byte)0, hid.ReadMode());
        Assert.Equal(new ClockOffsets(100, 100), hid.ReadOverclockProfile(1));
        Assert.Null(hid.ReadOverclockProfile(3));
        Assert.True(hid.WriteBacklightTimeout(70, 0));
        Assert.Equal((70, 0), hid.ReadBacklightTimeout());
    }
}

/// <summary>A 2024 Predator whose embedded controller takes the modes over HID, as Quick Access drives it.</summary>
public sealed class EcHidControlTests : IDisposable
{
    private static readonly OperatingMode[] AllFive =
        [OperatingMode.Eco, OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo];

    private readonly FakeHidBus _bus = new();
    private readonly FakeEcHid _ec = new();
    private readonly FakeFirmware _firmware = new() { SupportsModes = true };
    private readonly FakePower _power = new();

    public EcHidControlTests() => _bus.Devices.Add(_ec);

    public void Dispose()
    {
        _ec.Dispose();
        _firmware.Dispose();
    }

    private AcerDevice Device() => new(_firmware) { EcHid = EcHidDevice.Open(_bus, _ => { }, machineLock: false) };

    private FanControlService Service(ControlProfile profile, DirectSensors? sensors = null)
    {
        var device = Device();
        return new FanControlService(device, CapabilityProbe.Probe(device), new FakeLoad(), _power, profile, sensors);
    }

    [Fact]
    public void The_probe_takes_modes_profiles_and_the_backlight_timeout_from_the_controller()
    {
        var caps = CapabilityProbe.Probe(Device());

        Assert.Equal(AllFive, caps.OperatingModes);
        Assert.Equal(ModeRules.EmbeddedController, caps.ModeRules);
        Assert.Equal(new EcHidVersion(0, 7), caps.EcHid!.Version);
        Assert.Equal(3, caps.EcHid.OverclockProfiles);
        Assert.Equal(new ClockOffsets(150, 200), caps.EcHid.GpuOffsets[OperatingMode.Turbo]);
        Assert.Equal(new ClockOffsets(100, 100), caps.EcHid.GpuOffsets[OperatingMode.Performance]);
        Assert.Equal(new ClockOffsets(50, 0), caps.EcHid.GpuOffsets[OperatingMode.Balanced]);
        Assert.False(caps.EcHid.GpuOffsets.ContainsKey(OperatingMode.Quiet));
        Assert.True(caps.BatteryBoostFlag);
        Assert.True(caps.Keyboard.EcHidBacklightTimeout);
        Assert.True(caps.Keyboard.BacklightAutoOff);
        Assert.Contains("EC HID version: 0.7", caps.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("EC HID overclock profile 0 (Turbo) = core +150 MHz, memory +200 MHz", caps.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void A_controller_offering_one_mode_leaves_the_modes_to_WMI()
    {
        _ec.Status[EcHidStatus.ModeCapability] = 2;
        var caps = CapabilityProbe.Probe(Device());

        Assert.Empty(caps.EcHid!.Modes);
        Assert.Equal([OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo], caps.OperatingModes);
        Assert.Equal(ModeRules.Gaming, caps.ModeRules);
    }

    [Fact]
    public void Without_overclock_profiles_Acers_gaming_rules_apply()
    {
        _ec.Status.Remove(EcHidStatus.OverclockProfiles);
        var caps = CapabilityProbe.Probe(Device());

        Assert.Equal(AllFive, caps.OperatingModes);
        Assert.Equal(ModeRules.Gaming, caps.ModeRules);
        Assert.Empty(caps.EcHid!.GpuOffsets);
    }

    [Fact]
    public void Modes_are_set_on_the_controller_and_then_through_WMI()
    {
        var service = Service(new ControlProfile { OperatingMode = OperatingMode.Turbo });
        service.Tick();

        Assert.Equal([(byte)0], _ec.ModesSet);
        Assert.Equal(OperatingMode.Turbo, _firmware.Mode);
        Assert.Equal(OperatingMode.Turbo, service.Latest!.OperatingMode);
    }

    [Fact]
    public void A_weak_USB_C_adapter_holds_the_modes_back_until_a_stronger_one_is_in()
    {
        _ec.Status[EcHidStatus.Adapter] = 4;
        _ec.Status[EcHidStatus.UsbCAdapter] = 0;
        var service = Service(new ControlProfile { OperatingMode = OperatingMode.Turbo });

        service.Tick();
        Assert.Equal(PowerLimit.Adapter, service.Latest!.PowerLimit);
        Assert.Equal(OperatingMode.Balanced, service.Latest.OperatingMode);
        Assert.Equal((byte)2, _ec.Mode);

        _ec.Status[EcHidStatus.Adapter] = 1; // the laptop's own adapter
        service.OnPowerSourceChanged();
        service.Tick();
        Assert.Equal(PowerLimit.None, service.Latest.PowerLimit);
        Assert.Equal(OperatingMode.Turbo, service.Latest.OperatingMode);
    }

    [Fact]
    public void On_battery_only_Quiet_and_Balanced_run()
    {
        _power.IsOnAcPower = false;
        var service = Service(new ControlProfile { OperatingMode = OperatingMode.Turbo, BatteryOperatingMode = OperatingMode.Eco });

        service.Tick();
        Assert.Equal(PowerLimit.Battery, service.Latest!.PowerLimit);
        Assert.Equal(OperatingMode.Balanced, service.Latest.OperatingMode);
    }

    [Fact]
    public void The_battery_boost_flag_comes_from_the_controller()
    {
        _ec.Status[EcHidStatus.BatteryBoost] = 0;
        var service = Service(new ControlProfile { OperatingMode = OperatingMode.Performance });

        service.Tick();
        Assert.Equal(PowerLimit.LowBattery, service.Latest!.PowerLimit);

        _ec.Status[EcHidStatus.BatteryBoost] = 1;
        service.OnBatteryBoostEvent(); // the controller is read again
        service.Tick();
        Assert.Equal(PowerLimit.None, service.Latest.PowerLimit);
        Assert.Equal(OperatingMode.Performance, service.Latest.OperatingMode);
    }

    [Fact]
    public void The_firmwares_overclock_for_the_mode_is_added_to_the_users()
    {
        var driver = new FakeGpuClocks();
        var gpu = new FakeGpuSensor();
        var sensors = new DirectSensors(null, gpu, new FakeGpuPowerState { On = true }, SensorStatus.NotUsed, gpu.Status, driver);
        var profile = new ControlProfile
        {
            OperatingMode = OperatingMode.Turbo,
            GpuClocks = new GpuClockSettings { Modes = new Dictionary<OperatingMode, ClockOffsets> { [OperatingMode.Turbo] = new(15, 100) } },
        };
        var service = Service(profile, sensors);

        service.Tick();
        Assert.Equal(new ClockOffsets(165, 300), driver.Offsets);

        service.Update(profile with { GpuClocks = profile.GpuClocks with { FollowFirmware = false } });
        service.Tick();
        Assert.Equal(new ClockOffsets(15, 100), driver.Offsets);

        service.Update(profile with { OperatingMode = OperatingMode.Quiet });
        service.Tick();
        Assert.Equal(ClockOffsets.None, driver.Offsets); // Quiet has neither
    }

    [Fact]
    public void Backlight_auto_off_goes_through_the_controller_and_keeps_the_brightness()
    {
        _ec.Backlight = (60, 30);
        var device = Device();
        var caps = CapabilityProbe.Probe(device);

        Assert.True(KeyboardState.Read(device, caps.Keyboard).BacklightAutoOff);
        Assert.True(device.SetBacklightTimeout(caps.Keyboard, 60, 0));
        Assert.Equal((60, 0), _ec.Backlight);
        Assert.False(KeyboardState.Read(device, caps.Keyboard).BacklightAutoOff);
    }
}

/// <summary>The rules the embedded controller's models follow (Acer Quick Access's).</summary>
public sealed class EcHidModePolicyTests
{
    private static readonly OperatingMode[] AllFive =
        [OperatingMode.Eco, OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo];

    private static readonly ControlProfile Cycle = new();

    [Theory]
    [InlineData(PowerLimit.Battery)]
    [InlineData(PowerLimit.LowBattery)]
    [InlineData(PowerLimit.Adapter)]
    public void Short_power_allows_Quiet_and_Balanced(PowerLimit limit) =>
        Assert.Equal([OperatingMode.Quiet, OperatingMode.Balanced], OperatingModePolicy.Allowed(AllFive, limit, ModeRules.EmbeddedController));

    [Fact]
    public void Acers_gaming_rules_keep_Eco_on_battery_and_hold_back_for_a_weak_adapter_too()
    {
        Assert.Equal([OperatingMode.Eco, OperatingMode.Quiet, OperatingMode.Balanced], OperatingModePolicy.Allowed(AllFive, PowerLimit.Battery));
        Assert.Equal([OperatingMode.Quiet, OperatingMode.Balanced], OperatingModePolicy.Allowed(AllFive, PowerLimit.Adapter));
    }

    [Fact]
    public void A_weak_adapter_comes_before_a_low_battery() =>
        Assert.Equal(PowerLimit.Adapter, OperatingModePolicy.Limit(onAc: true, batteryBoost: false, weakAdapter: true));

    [Theory]
    [InlineData(OperatingMode.Balanced, OperatingMode.Performance)]
    [InlineData(OperatingMode.Performance, OperatingMode.Turbo)]
    [InlineData(OperatingMode.Turbo, OperatingMode.Eco)]
    [InlineData(OperatingMode.Eco, OperatingMode.Quiet)]
    [InlineData(OperatingMode.Quiet, OperatingMode.Balanced)]
    public void The_Mode_key_goes_round_the_controllers_order(OperatingMode from, OperatingMode to) =>
        Assert.Equal(to, OperatingModePolicy.NextForKey(Cycle, from, AllFive, PowerLimit.None, ModeRules.EmbeddedController));

    [Fact]
    public void With_power_short_the_key_toggles_Quiet_and_Balanced_and_starts_from_Balanced()
    {
        Assert.Equal(OperatingMode.Quiet,
            OperatingModePolicy.NextForKey(Cycle, OperatingMode.Balanced, AllFive, PowerLimit.Battery, ModeRules.EmbeddedController));
        Assert.Equal(OperatingMode.Balanced,
            OperatingModePolicy.NextForKey(Cycle, OperatingMode.Quiet, AllFive, PowerLimit.Adapter, ModeRules.EmbeddedController));
        Assert.Equal(OperatingMode.Balanced,
            OperatingModePolicy.NextForKey(Cycle, OperatingMode.Turbo, AllFive, PowerLimit.Battery, ModeRules.EmbeddedController));
    }

    [Fact]
    public void Turbo_on_the_key_is_refused_with_a_weak_adapter()
    {
        var turbo = new ControlProfile { ModeKey = ModeKeyAction.TurboToggle };
        Assert.True(OperatingModePolicy.TurboBlocked(turbo, PowerLimit.Adapter));
        Assert.Null(OperatingModePolicy.NextForKey(turbo, OperatingMode.Balanced, AllFive, PowerLimit.Adapter, ModeRules.EmbeddedController));
    }

    [Fact]
    public void The_firmwares_offsets_add_to_the_users_while_followed()
    {
        var firmware = new Dictionary<OperatingMode, ClockOffsets> { [OperatingMode.Turbo] = new(150, 200) };
        var clocks = new GpuClockSettings { Modes = new Dictionary<OperatingMode, ClockOffsets> { [OperatingMode.Turbo] = new(-30, 0) } };

        Assert.Equal(new ClockOffsets(120, 200), clocks.For(true, OperatingMode.Turbo, firmware));
        Assert.Equal(new ClockOffsets(-30, 0), (clocks with { FollowFirmware = false }).For(true, OperatingMode.Turbo, firmware));
        Assert.Equal(ClockOffsets.None, clocks.For(true, OperatingMode.Quiet, firmware));
        Assert.Equal(ClockOffsets.None, clocks.For(false, OperatingMode.Turbo, firmware)); // without modes: the fixed ones
    }
}

/// <summary>A 2024 Predator with the embedded controller's HID interface, end to end over the real JSON-RPC stack.</summary>
public sealed class EcHidIpcTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
    private readonly SimulatedMachine _machine = new(SimulatedModel.Predator2024);
    private readonly FakeEcHid _ec = new();
    private OpenSenseEngine _engine = null!;
    private StreamJsonRpc.JsonRpc _server = null!;
    private StreamJsonRpc.JsonRpc _client = null!;
    private IOpenSenseService _service = null!;

    public ValueTask InitializeAsync()
    {
        _machine.Hid.Devices.Add(_ec);
        _engine = new OpenSenseEngine(_machine, Path.Combine(_directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        _engine.Start();
        (_server, _client, _service) = IpcTests.Serve(_engine);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task The_controllers_capabilities_cross_the_wire_and_its_Mode_key_order_applies()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshot = await _service.GetSnapshotAsync(ct);
        var ecHid = snapshot.Capabilities.EcHid!;
        Assert.Equal(new EcHidVersion(0, 7), ecHid.Version);
        Assert.Equal([OperatingMode.Eco, OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo], ecHid.Modes);
        Assert.Equal(new ClockOffsets(150, 200), ecHid.GpuOffsets[OperatingMode.Turbo]);
        Assert.Equal(ModeRules.EmbeddedController, snapshot.Capabilities.ModeRules);
        Assert.True(snapshot.Settings.Profile.GpuClocks.FollowFirmware);

        await _service.SetProfileAsync(snapshot.Settings.Profile with { OperatingMode = OperatingMode.Turbo }, ct);
        var turbo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.OperatingMode == OperatingMode.Turbo)
                turbo.TrySetResult();
        };
        await turbo.Task.WaitAsync(Timeout, ct);

        var switched = new TaskCompletionSource<ControlNotice>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.NoticeRaised += (_, n) =>
        {
            if (n.Kind == NoticeKind.OperatingModeSwitchedByKey)
                switched.TrySetResult(n);
        };
        _machine.Events.Raise(7, 4, 0, 0);

        // Round the controller's order: after Turbo comes Eco (Acer's gaming rules would go to Quiet).
        Assert.Equal(OperatingMode.Eco, (await switched.Task.WaitAsync(Timeout, ct)).OperatingMode);
        var eco = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.TelemetryUpdated += (_, t) =>
        {
            if (t.OperatingMode == OperatingMode.Eco)
                eco.TrySetResult();
        };
        await eco.Task.WaitAsync(Timeout, ct);
        Assert.Equal((byte)4, _ec.Mode);
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
