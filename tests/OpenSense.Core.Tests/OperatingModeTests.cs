using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

/// <summary>
/// Firmware that lists operating modes but also has CoolBoost (the AN515-57 generation) uses CoolBoost, as
/// NitroSense does, even when NitroSense is not installed; the user can switch between them either way.
/// </summary>
public sealed class OperatingModeTests
{
    private static readonly OperatingMode[] Listed = [OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance, OperatingMode.Turbo];

    [Fact]
    public void CoolBoost_firmware_keeps_listed_modes_off_without_NitroSense()
    {
        var caps = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { SupportsModes = true, SupportsCoolBoost = true }));

        Assert.False(caps.HasOperatingModes);
        Assert.Equal(Listed, caps.FirmwareOperatingModes);
        Assert.True(caps.CoolBoost);
        Assert.True(caps.FirmwareCoolBoost);
    }

    [Fact]
    public void Firmware_without_CoolBoost_offers_its_modes()
    {
        var caps = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { SupportsModes = true }));

        Assert.Equal(Listed, caps.OperatingModes);
        Assert.False(caps.CoolBoost);
    }

    [Fact]
    public void Modes_can_be_turned_on_and_off_and_CoolBoost_follows()
    {
        var detected = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { SupportsModes = true, SupportsCoolBoost = true }));

        var on = new CapabilityOverrides { OperatingModes = true }.Apply(detected);
        Assert.Equal(Listed, on.OperatingModes);
        Assert.False(on.CoolBoost);

        var off = new CapabilityOverrides { OperatingModes = false }.Apply(on with { });
        Assert.False(off.HasOperatingModes);
        Assert.True(off.CoolBoost);

        // Forcing modes off on firmware that offers them by default (and has no CoolBoost).
        var modesModel = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware { SupportsModes = true }));
        var forcedOff = new CapabilityOverrides { OperatingModes = false }.Apply(modesModel);
        Assert.False(forcedOff.HasOperatingModes);
        Assert.False(forcedOff.CoolBoost);
    }

    [Fact]
    public void Turning_modes_off_returns_the_firmware_to_its_default_mode()
    {
        var firmware = new FakeFirmware { SupportsModes = true, SupportsCoolBoost = true };
        var device = new AcerDevice(firmware);
        var caps = CapabilityProbe.Probe(device);
        device.SetOperatingMode(OperatingMode.Turbo); // chosen while the modes were on

        var service = new FanControlService(device, caps, new FakeLoad(), new FakePower(), new ControlProfile { OperatingMode = OperatingMode.Turbo });
        service.Tick();

        Assert.Equal(OperatingMode.Balanced, firmware.Mode);
        Assert.Null(service.Latest!.FanLock); // no Quiet-style fan lock either
    }
}
