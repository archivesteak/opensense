using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Tests;

/// <summary>
/// The ANV15-51's BIOS package holds two boards. ZYP: misc 0x0B modes, fan speeds that are duties, keyboard methods and
/// function 7 that are stubs. ZYQ: the EC HID interface's modes and brightness, fan speeds that are boosts, a misc 9 that
/// can list GPU modes misc 2 can't set. Generic detection gave ZYP a dead CoolBoost and brightness slider instead of its
/// modes, and treated its duties as boosts (a fan slowed down near the limit).
/// </summary>
public sealed class NitroV15Tests
{
    private const string Model = "Nitro ANV15-51";

    [Fact]
    public void Zyp_offers_its_modes_runs_its_fans_as_duties_and_drops_the_dead_switches()
    {
        var firmware = new FakeFirmware { SupportsModes = true, ModeMask = 0x53, SupportsCoolBoost = true, SupportsBacklightHotkey = true };
        var smbios = new AcerSmbios(2, 0x5D, [new(0x08, 1), new(0x0A, 1)], [new AcerSmbiosRecord(0x84, 8, 2)]);
        var generic = CapabilityProbe.Probe(new AcerDevice(firmware), smbios, Model);
        var device = new AcerDevice(firmware) { FirmwareProfile = AcerFirmwareProfile.For(Model, "V1.53") };

        var caps = CapabilityProbe.Probe(device, smbios, Model);

        Assert.True(generic.CoolBoost);
        Assert.False(generic.HasOperatingModes);
        Assert.Single(generic.Lights);
        Assert.Equal([OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Eco], caps.OperatingModes);
        Assert.False(caps.CoolBoost);
        Assert.Empty(caps.Lights);
        Assert.True(caps.Keyboard.BacklightAutoOff);
        Assert.True(caps.FixedFanSpeeds);

        // A Custom duty under the floor would leave the fan nearly stopped, with nothing in the EC to speed it up.
        var profile = new ControlProfile
        {
            Mode = FanControlMode.Custom,
            Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(10), [FanId.Gpu] = new(60) },
        };
        firmware.CpuTemp = firmware.GpuTemp = 50;
        new FanControlService(device, caps, new FakeLoad(), new FakePower(), profile).Tick();
        Assert.Equal(30, firmware.Speed[1]);
        Assert.Equal(60, firmware.Speed[4]);
    }

    [Fact]
    public void Zyq_keeps_its_boosts_and_brightness_and_offers_no_GPU_switch_it_cannot_set()
    {
        var firmware = new FakeFirmware { SupportsCoolBoost = true, SupportsBacklightHotkey = true, GpuModes = 7, GpuMode = 0xFF };
        var smbios = new AcerSmbios(2, 0x5E, [new(0x08, 1), new(0x0A, 1)], [new AcerSmbiosRecord(0x84, 8, 2)]);
        var bus = new FakeHidBus();
        var controller = new FakeEcHid { ReportsLevels = true };
        bus.Devices.Add(controller);
        using var hid = EcHidDevice.Open(bus, _ => { }, machineLock: false)!;
        var device = new AcerDevice(firmware) { EcHid = hid, FirmwareProfile = AcerFirmwareProfile.For(Model, "V1.53", ecHid: true) };

        var caps = CapabilityProbe.Probe(device, smbios, Model);

        Assert.False(caps.FixedFanSpeeds);
        Assert.Contains(OperatingMode.Turbo, caps.OperatingModes);
        Assert.False(caps.CoolBoost);
        Assert.False(caps.GpuModeSwitch);
        Assert.Equal(LightingBackendKind.EcKeyboardBrightness, Assert.Single(caps.Lights).Backend);

        // The controller reports 25 % as 50 %: switching auto-off must not raise the brightness with it.
        Assert.True(device.SetBacklightTimeout(caps.Keyboard, 25, 0));
        Assert.Equal((25, 0), device.GetBacklightTimeout(caps.Keyboard));
        controller.Backlight = (100, 0); // the Fn keys
        Assert.Equal((100, 0), device.GetBacklightTimeout(caps.Keyboard));
    }
}
