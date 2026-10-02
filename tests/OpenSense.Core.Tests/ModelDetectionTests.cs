using OpenSense.Core.Hardware;

namespace OpenSense.Core.Tests;

public sealed class ModelDetectionTests
{
    [Theory]
    [InlineData("AN515-57", false)]
    [InlineData("AN515-58", true)]
    [InlineData("AN517-43", true)]
    public void Built_in_defaults_preserve_legacy_mode_choices_without_installer_data(string model, bool modes)
    {
        using var firmware = new FakeFirmware { SupportsModes = true, SupportsCoolBoost = true };
        var smbios = new AcerSmbios(null, null, [new(0x0F, 1)], []);
        var caps = CapabilityProbe.Probe(new(firmware), smbios, model);

        Assert.Equal(modes, caps.HasOperatingModes);
        Assert.Equal(!modes, caps.CoolBoost);
        Assert.NotEmpty(caps.FirmwareOperatingModes);
        Assert.Empty(firmware.Writes);
    }

    [Fact]
    public void Catalog_does_not_invent_modes_when_the_firmware_does_not_answer()
    {
        using var firmware = new FakeFirmware();
        var caps = CapabilityProbe.Probe(new(firmware), model: "AN515-58");

        Assert.Empty(caps.OperatingModes);
        Assert.Empty(caps.FirmwareOperatingModes);
    }

    [Fact]
    public void Unknown_model_uses_smbios_mode_support_instead_of_a_successful_coolboost_stub()
    {
        using var firmware = new FakeFirmware { SupportsModes = true, SupportsCoolBoost = true };
        var caps = CapabilityProbe.Probe(new(firmware), new(null, null, [new(0x0F, 1)], []), "New model");

        Assert.NotEmpty(caps.OperatingModes);
        Assert.False(caps.CoolBoost);
    }

    [Theory]
    [InlineData(0x81)]
    [InlineData(0x8A)]
    [InlineData(0x9F)]
    public void Timeout_uses_the_units_advertised_function_and_verifies_it(byte function)
    {
        using var firmware = new TimeoutFirmware(function);
        var caps = CapabilityProbe.Probe(new(firmware), new(null, null, [], [new(function, 8)]), "New model");

        Assert.Equal(function, caps.Keyboard.BacklightHotkey);
        Assert.True(caps.Keyboard.BacklightAutoOff);
        Assert.Contains(caps.Lights, light => light.Backend == OpenSense.Core.Lighting.LightingBackendKind.EcKeyboardBrightness);
        Assert.Contains(("GetFunction", 0x80001UL | (ulong)function << 8), firmware.Base.Calls);
        Assert.Empty(firmware.Base.Writes);
    }

    [Fact]
    public void Hotkey_selection_ignores_other_functions_and_preserves_table_order()
    {
        var smbios = new AcerSmbios(null, null, [],
            [new(0x80, 8), new(0x81, 8), new(0x84, 8), new(0x8A, 8), new(0x8B, 4), new(0xA0, 8)]);

        Assert.Equal((byte)0x8A, smbios.BacklightHotkey);
        Assert.Null(new AcerSmbios(null, null, [], [new(0x84, 4)]).BacklightHotkey);
    }

    [Fact]
    public void Advertised_timeout_that_does_not_answer_is_not_exposed()
    {
        using var firmware = new FakeFirmware();
        var caps = CapabilityProbe.Probe(new(firmware), new(null, null, [], [new(0x8A, 8)]), "AN515-57");

        Assert.Null(caps.Keyboard.BacklightHotkey);
        Assert.False(caps.Keyboard.BacklightAutoOff);
    }

    [Fact]
    public void Measured_timeout_fallback_works_without_smbios_but_does_not_override_it()
    {
        using var firmware = new TimeoutFirmware(0x84);
        Assert.Equal((byte)0x84, CapabilityProbe.Probe(new(firmware), model: "AN515-57").Keyboard.BacklightHotkey);
        Assert.Null(CapabilityProbe.Probe(new(firmware), new(null, null, [], [new(0x8A, 8)]), "AN515-57").Keyboard.BacklightHotkey);
    }

    [Theory]
    [InlineData("AN515-52")]
    [InlineData("New model")]
    public void Keyboard_lock_and_overdrive_follow_live_replies_even_without_model_flags(string model)
    {
        using var firmware = new FakeFirmware { Profile = 0x0000_0001_0103_FF00 };
        var caps = CapabilityProbe.Probe(new(firmware), model: model);

        Assert.True(caps.Keyboard.WindowsKey);
        Assert.True(caps.Keyboard.LcdOverdrive);
        firmware.Profile = 0x00FF_0000_FFFF_FF00;
        var unsupported = CapabilityProbe.Probe(new(firmware), model: model);
        Assert.False(unsupported.Keyboard.WindowsKey);
        Assert.False(unsupported.Keyboard.LcdOverdrive);
    }

    [Fact]
    public void Valid_sensor_bitmap_wins_over_model_fan_counts()
    {
        using var firmware = new FakeFirmware();
        var caps = CapabilityProbe.Probe(new(firmware), model: "Predator PTX17-71");

        Assert.DoesNotContain(FanChannel.Gpu2, caps.Fans);
        Assert.DoesNotContain(firmware.Calls, c => c.Method == "GetGamingSysInfo" && c.Input == AcerProtocol.SensorReadInput(SensorId.Gpu2FanSpeed));
    }

    [Fact]
    public void Known_fan_counts_remain_available_when_sensor_discovery_fails()
    {
        using var firmware = new FakeFirmware { Fault = new AcerWmiException("Sensor discovery unavailable") };
        var caps = CapabilityProbe.Probe(new(firmware), model: "Predator PTX17-71");

        Assert.Equal([FanChannel.Cpu, FanChannel.Gpu, FanChannel.Gpu2], caps.Fans);
        Assert.Contains(SensorId.GpuTemperature, caps.Sensors);
    }

    [Fact]
    public void An_AN515_45_offers_only_the_effects_its_controller_has()
    {
        // Its EC has no handler for Meteor or Twinkling: the keyboard would go dark.
        using var firmware = new FakeFirmware();
        var rgb = new AcerSmbios(null, null, [new(0x0A, 2)], []);

        Assert.Equal(KeyboardProtocol.FiveZonedEffects, CapabilityProbe.Probe(new(firmware), rgb, "Nitro AN515-45").Keyboard.Effects);
        Assert.Equal(KeyboardProtocol.ZonedEffects, CapabilityProbe.Probe(new(firmware), rgb, "Nitro AN515-57").Keyboard.Effects);
    }

    [Fact]
    public void The_gpu_switch_cannot_be_forced_on_firmware_that_lists_no_gpu_modes()
    {
        // The AN515-45's SMM code writes a misc 2 value into the embedded controller's own flags.
        using var firmware = new FakeFirmware();
        var caps = new CapabilityOverrides { GpuModeSwitch = true }.Apply(CapabilityProbe.Probe(new(firmware), model: "Nitro AN515-45"));

        Assert.False(caps.GpuModeSwitch);
    }

    private sealed class TimeoutFirmware(byte function) : IWmiTransport
    {
        public FakeFirmware Base { get; } = new();
        public bool IsClassAvailable(string className) => Base.IsClassAvailable(className);
        public bool HasArrayInput(string className, string method) => Base.HasArrayInput(className, method);
        public IReadOnlyList<string> ReadStrings(string className, string propertyName) => Base.ReadStrings(className, propertyName);
        public WmiArrayResult InvokeArray(string className, string method, byte[]? input) => Base.InvokeArray(className, method, input);
        public WmiOutputs InvokeNamed(string className, string method, IReadOnlyList<WmiArgument> inputs) => Base.InvokeNamed(className, method, inputs);
        public ulong Invoke(string className, string method, ulong input)
        {
            var result = Base.Invoke(className, method, input);
            return className == AcerProtocol.ActionClass && method == "GetFunction" && input == (0x80001UL | (ulong)function << 8)
                ? 0x1E64_0000_0000UL : result;
        }
        public void Dispose() => Base.Dispose();
    }
}
