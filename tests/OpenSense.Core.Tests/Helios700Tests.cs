using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

/// <summary>
/// The PH717-71's BIOS answers keyboard settings it has nothing behind: its auto-off function keeps a flag nothing reads
/// (and ignores the brightness), overdrive reads 0 unhandled, and its EC's Windows key lock only reaches a PS/2 keyboard,
/// while the laptop's is a USB one. Issue #8: the brightness light that came from them refused every change.
/// </summary>
public sealed class Helios700Tests
{
    private const string Model = "Predator PH717-71";

    [Fact]
    public void Its_bios_offers_no_keyboard_setting_it_has_nothing_behind()
    {
        var firmware = new FakeFirmware { SupportsBacklightHotkey = true, Profile = 0x101000000 };
        var smbios = new AcerSmbios(2, 0x44, [new(0x08, 0), new(0x0A, 2)], [new AcerSmbiosRecord(0x84, 8, 2)]);
        var generic = CapabilityProbe.Probe(new AcerDevice(firmware), smbios, Model);
        var device = new AcerDevice(firmware) { FirmwareProfile = AcerFirmwareProfile.For(Model, "V1.10") };

        var caps = CapabilityProbe.Probe(device, smbios, Model);

        Assert.True(generic.Keyboard.BacklightAutoOff && generic.Keyboard.WindowsKey && generic.Keyboard.LcdOverdrive);
        Assert.Single(generic.Lights);
        Assert.Empty(caps.Lights);
        Assert.False(caps.Keyboard.BacklightAutoOff);
        Assert.False(caps.Keyboard.WindowsKey);
        Assert.False(caps.Keyboard.LcdOverdrive);
        Assert.True(caps.FixedFanSpeeds);
    }
}
