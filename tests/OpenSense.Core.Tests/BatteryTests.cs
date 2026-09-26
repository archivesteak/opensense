using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Tests;

public class BatteryProtocolTests
{
    [Fact]
    public void Status_and_set_calls_name_every_parameter()
    {
        Assert.Equal(["uBatteryNo", "uFunctionQuery", "uReserved"], BatteryProtocol.StatusArguments().Select(a => a.Name));
        var set = BatteryProtocol.SetArguments(BatteryFunction.Calibration, true).ToDictionary(a => a.Name, a => a.Value);
        Assert.Equal((byte)1, set["uBatteryNo"]);
        Assert.Equal((byte)2, set["uFunctionMask"]);
        Assert.Equal((byte)1, set["uFunctionStatus"]);
        Assert.Equal(new byte[5], set["uReservedIn"]);
    }

    [Fact]
    public void Status_decodes_what_the_AN515_57_answers()
    {
        // Health mode on (measured 2026-09-24).
        var status = BatteryProtocol.DecodeStatus(FakeFirmware.Outputs(
            ("uFunctionList", 3UL), ("uFunctionStatus", new byte[] { 1, 0, 0, 0, 0 }), ("uReturn", new byte[] { 0, 0 })));

        Assert.Equal(new BatteryHealthStatus(true, true, true, false), status);
        Assert.Null(BatteryProtocol.DecodeStatus(FakeFirmware.Outputs(
            ("uFunctionList", 3UL), ("uFunctionStatus", new byte[5]), ("uReturn", new byte[] { 1, 0 }))));
    }

    [Theory]
    [InlineData(true, 20, 0x140F04UL)]
    [InlineData(true, 30, 0x1E0F04UL)]
    [InlineData(false, 30, 0x1E1F04UL)]
    public void Usb_charging_input_matches_the_firmware(bool on, int floor, ulong expected) =>
        Assert.Equal(expected, AcerProtocol.UsbChargingInput(on, floor));

    [Fact]
    public void Usb_charging_is_on_only_in_state_0F()
    {
        Assert.Equal(new UsbChargingState(false, null), AcerProtocol.UsbChargingValue(0x6400)); // never set
        Assert.Equal(new UsbChargingState(true, 20), AcerProtocol.UsbChargingValue(0x140F00));
        Assert.Equal(new UsbChargingState(false, 30), AcerProtocol.UsbChargingValue(0x1E1F00));
    }

    [Fact]
    public void Battery_boost_flag_is_byte_5() =>
        Assert.True(AcerProtocol.BatteryBoostValue(0x10000000000));

    [Fact]
    public void Probe_finds_the_battery_functions_and_usb_charging()
    {
        var caps = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { SupportsBattery = true, SupportsUsbCharging = true }));
        var none = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware()));

        Assert.Equal(new BatteryCapabilities(true, true), caps.Battery);
        Assert.True(caps.UsbCharging);
        Assert.Equal(BatteryCapabilities.None, none.Battery);
        Assert.False(none.UsbCharging);
    }
}

public class PowerServiceTests
{
    [Fact]
    public async Task Settings_already_in_the_firmware_are_not_written_again()
    {
        var (service, firmware, _, _) = Create();
        firmware.HealthMode = true;
        firmware.UsbCharging = 0x140F00;

        await service.ApplyAsync(new PowerSettings { ChargeLimit = true, UsbCharging = new UsbChargingSettings(true, 20) });

        Assert.DoesNotContain(firmware.NamedCalls, c => c.Method == "SetBatteryHealthControl");
        Assert.DoesNotContain(firmware.Writes, w => w.Method == "SetFunction");
    }

    [Fact]
    public async Task Changed_settings_are_written_and_null_leaves_the_firmware_alone()
    {
        var (service, firmware, _, _) = Create();

        await service.ApplyAsync(new PowerSettings { ChargeLimit = true, UsbCharging = new UsbChargingSettings(false, 10) });
        Assert.True(firmware.HealthMode);
        Assert.Equal(0x0A1F00UL, firmware.UsbCharging);

        firmware.NamedCalls.Clear();
        await service.ApplyAsync(new PowerSettings());
        Assert.Empty(firmware.NamedCalls);
    }

    [Fact]
    public async Task A_refused_charge_limit_is_reported()
    {
        var (service, firmware, _, _) = Create();
        firmware.RejectEverything = true;
        var notices = new List<NoticeKind>();
        service.Notice += n => notices.Add(n.Kind);

        await service.ApplyAsync(new PowerSettings { ChargeLimit = true });

        Assert.Equal([NoticeKind.ChargeLimitRejected], notices);
    }

    [Fact]
    public async Task Calibration_needs_the_adapter()
    {
        var (service, firmware, _, power) = Create();
        power.IsOnAcPower = false;

        Assert.Equal(CalibrationResult.NeedsAc, await service.StartCalibrationAsync());
        Assert.False(firmware.Calibrating);
    }

    [Fact]
    public async Task Calibration_turns_the_limit_off_keeps_windows_awake_and_puts_everything_back_when_done()
    {
        var (service, firmware, system, _) = Create();
        firmware.HealthMode = true;
        CalibrationRecord? saved = null;
        var notices = new List<NoticeKind>();
        service.Notice += n => notices.Add(n.Kind);
        await service.ApplyAsync(new PowerSettings { ChargeLimit = true });

        Assert.Equal(CalibrationResult.Started, await service.StartCalibrationAsync());
        saved = service.Calibration;

        Assert.True(firmware.Calibrating);
        Assert.False(firmware.HealthMode);
        Assert.True(system.Awake);
        Assert.All(system.Values.Values, v => Assert.Equal(0u, v)); // lid and low-battery actions: do nothing
        Assert.NotNull(saved);
        Assert.True(saved.ChargeLimitBefore);
        Assert.Equal(CalibrationResult.AlreadyRunning, await service.StartCalibrationAsync());

        // The firmware finishes.
        firmware.Calibrating = false;
        await service.CheckCalibrationAsync();

        Assert.Null(service.Calibration);
        Assert.True(firmware.HealthMode);
        Assert.False(system.Awake);
        Assert.All(system.Values.Values, v => Assert.Equal(1u, v));
        Assert.Equal([NoticeKind.CalibrationFinished], notices);
    }

    [Fact]
    public async Task Unplugging_stops_the_calibration()
    {
        var (service, firmware, system, power) = Create();
        var notices = new List<NoticeKind>();
        service.Notice += n => notices.Add(n.Kind);
        await service.StartCalibrationAsync();

        power.IsOnAcPower = false;
        await service.CheckCalibrationAsync();

        Assert.False(firmware.Calibrating);
        Assert.False(system.Awake);
        Assert.Equal([NoticeKind.CalibrationStoppedUnplugged], notices);
    }

    [Fact]
    public async Task A_sleep_that_ended_the_calibration_is_reported_as_an_interruption()
    {
        var (service, firmware, _, _) = Create();
        var notices = new List<NoticeKind>();
        service.Notice += n => notices.Add(n.Kind);
        await service.StartCalibrationAsync();

        service.OnSuspend();
        firmware.Calibrating = false; // the sleep ended it
        await service.CheckCalibrationAsync(); // the first look after waking, whatever triggers it

        Assert.Equal([NoticeKind.CalibrationInterrupted], notices);
    }

    [Fact]
    public async Task A_calibration_that_goes_on_after_a_sleep_is_still_followed()
    {
        var (service, firmware, _, _) = Create();
        var notices = new List<NoticeKind>();
        service.Notice += n => notices.Add(n.Kind);
        await service.StartCalibrationAsync();

        service.OnSuspend();
        await service.CheckCalibrationAsync(); // still calibrating after waking
        firmware.Calibrating = false;
        await service.CheckCalibrationAsync();

        Assert.Equal([NoticeKind.CalibrationFinished], notices);
    }

    [Fact]
    public async Task A_sleep_without_a_calibration_does_not_mark_the_next_one()
    {
        var (service, firmware, _, _) = Create();
        var notices = new List<NoticeKind>();
        service.Notice += n => notices.Add(n.Kind);

        service.OnSuspend();
        await service.StartCalibrationAsync();
        firmware.Calibrating = false;
        await service.CheckCalibrationAsync();

        Assert.Equal([NoticeKind.CalibrationFinished], notices);
    }

    [Fact]
    public async Task The_user_can_stop_a_calibration()
    {
        var (service, firmware, system, _) = Create();
        await service.StartCalibrationAsync();

        await service.StopCalibrationAsync();

        Assert.False(firmware.Calibrating);
        Assert.False(system.Awake);
        Assert.Null(service.Calibration);
    }

    [Fact]
    public async Task A_calibration_still_running_after_a_restart_is_picked_up()
    {
        var firmware = new FakeFirmware { SupportsBattery = true, Calibrating = true };
        var system = new FakeSystemPower();
        CalibrationRecord? stored = new(DateTime.UtcNow, ChargeLimitBefore: false, PowerScheme: null);
        using var service = new PowerService(new ImmediateDispatcher(new AcerDevice(firmware)), Caps, new FakePower(), system,
            stored, r => stored = r);

        await service.StartAsync(new PowerSettings());

        Assert.True(system.Awake);
        Assert.NotNull(stored?.PowerScheme);
        Assert.NotNull(service.Calibration);
    }

    [Fact]
    public async Task A_calibration_that_ended_while_OpenSense_was_stopped_is_cleaned_up()
    {
        var firmware = new FakeFirmware { SupportsBattery = true };
        var system = new FakeSystemPower();
        CalibrationRecord? stored = new(DateTime.UtcNow, ChargeLimitBefore: true, PowerScheme: null);
        using var service = new PowerService(new ImmediateDispatcher(new AcerDevice(firmware)), Caps, new FakePower(), system,
            stored, r => stored = r);
        var notices = new List<NoticeKind>();
        service.Notice += n => notices.Add(n.Kind);

        await service.StartAsync(new PowerSettings());

        Assert.Null(stored);
        Assert.True(firmware.HealthMode);
        Assert.Equal([NoticeKind.CalibrationInterrupted], notices);
    }

    [Fact]
    public async Task Stopping_the_engine_mid_calibration_puts_windows_back_but_keeps_the_record()
    {
        var firmware = new FakeFirmware { SupportsBattery = true };
        var system = new FakeSystemPower();
        CalibrationRecord? stored = null;
        var service = new PowerService(new ImmediateDispatcher(new AcerDevice(firmware)), Caps, new FakePower(), system,
            null, r => stored = r);
        await service.StartCalibrationAsync();

        service.Dispose();

        Assert.False(system.Awake);
        Assert.All(system.Values.Values, v => Assert.Equal(1u, v));
        Assert.NotNull(stored);
        Assert.Null(stored.PowerScheme);
    }

    private static readonly DeviceCapabilities Caps = DeviceCapabilities.None with
    {
        Battery = new BatteryCapabilities(true, true),
        UsbCharging = true,
    };

    private static (PowerService Service, FakeFirmware Firmware, FakeSystemPower System, FakePower Power) Create()
    {
        var firmware = new FakeFirmware { SupportsBattery = true, SupportsUsbCharging = true };
        var system = new FakeSystemPower();
        var power = new FakePower();
        var service = new PowerService(new ImmediateDispatcher(new AcerDevice(firmware)), Caps, power, system, null, _ => { });
        return (service, firmware, system, power);
    }
}
