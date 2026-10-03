using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

/// <summary>
/// The NL16-71G's APGeAction function 7 is the operating mode, as its own answer says, and its battery-boost flag a
/// constant 0. The Aspire A14-51GM is built from the same BIOS: what holds for one holds for the other, without a profile.
/// </summary>
public sealed class NitroLiteTests
{
    [Theory]
    [InlineData("Nitro NL16-71G")]
    [InlineData("Aspire A14-51GM")]
    public void Modes_go_through_function_7_and_are_not_held_back_on_AC(string model)
    {
        var firmware = new FakeFirmware { SensorMask = 0x205UL << 24, SupportsActionModes = true, BatteryBoost = false };
        var device = new AcerDevice(firmware) { FirmwareProfile = AcerFirmwareProfile.For(model, "V1.19") };

        var caps = CapabilityProbe.Probe(device, model: model);

        Assert.Empty(caps.Fans);
        Assert.Equal([OperatingMode.Quiet, OperatingMode.Balanced, OperatingMode.Performance], caps.OperatingModes);
        Assert.Equal(OperatingMode.Performance, FirmwareState.Read(device, caps).OperatingMode);
        // A CoolBoost write would switch the mode: none, not even with the modes turned off.
        Assert.False(caps.CoolBoost);
        Assert.False(new CapabilityOverrides { OperatingModes = false }.Apply(caps).CoolBoost);

        var service = new FanControlService(device, caps, new FakeLoad(), new FakePower(), new ControlProfile { OperatingMode = OperatingMode.Performance });
        service.Tick();

        Assert.Equal(("SetFunction", 0x030007UL), Assert.Single(firmware.Writes));
        Assert.Equal(PowerLimit.None, service.Latest!.PowerLimit);
    }
}
