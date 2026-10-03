using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Tests;

public sealed class NitroV16LightingTests
{
    private static readonly KeyboardCapabilities Rgb = new()
    {
        RgbBacklight = true,
        Zones = 4,
        Effects = KeyboardProtocol.ZonedEffects,
    };

    [Theory]
    [InlineData("V1.50.1")]
    [InlineData("V2.1.50")]
    public void Unreviewed_dotted_bios_revision_keeps_conservative_password_length(string version) =>
        Assert.Equal(16, AcerFirmwareProfile.For("ANV16-72", version).BiosPasswordMaxLength);

    [Theory]
    [InlineData("ANV16-A31")]
    [InlineData("ANV16-A71")]
    [InlineData("ANV16S-41")]
    [InlineData("ANV16S-61")]
    public void Mailbox_only_rgb_does_not_override_brightness_control(string model)
    {
        var caps = CapabilityProbe.Probe(Device(new LightingFirmware(), model), new(null, null, [new(0x0A, 2)], []), model);

        Assert.False(caps.Keyboard.RgbBacklight);
        Assert.True(caps.Keyboard.WmiBacklightBrightness);
        Assert.Contains(caps.Lights, l => l.Backend == LightingBackendKind.EcKeyboardBrightness && l.Zones == 0);
        Assert.DoesNotContain(caps.Lights, l => l.Backend == LightingBackendKind.EcKeyboard);
    }

    [Theory]
    [InlineData("ANV16-72")]
    [InlineData("ANV16-I31")]
    public void Unsupported_wmi_controls_stay_disabled_even_with_smbios_rgb_and_overrides(string model)
    {
        var caps = CapabilityProbe.Probe(Device(new LightingFirmware(), model), new(null, null, [new(0x0A, 2), new(0x0F, 1)], []), model);
        var forced = new CapabilityOverrides { OperatingModes = true }.Apply(caps);

        Assert.False(caps.Keyboard.RgbBacklight);
        Assert.False(forced.CoolBoost);
        Assert.Empty(forced.OperatingModes);
    }

    [Fact]
    public void The_ANV16_41s_rgb_zones_are_driven_though_its_bios_reports_a_single_colour()
    {
        // Record 0x0A is 1 on every ANV16-41 (a fixed default), yet its EC drives effects, colours and zones.
        var caps = CapabilityProbe.Probe(Device(new LightingFirmware(), "ANV16-41"), new(null, null, [new(0x08, 4), new(0x0A, 1)], []), "ANV16-41");

        Assert.True(caps.Keyboard.RgbBacklight);
        Assert.Equal(4, caps.Keyboard.Zones);
        Assert.False(caps.Keyboard.ZoneSwitches);
        Assert.Contains(caps.Lights, l => l.Backend == LightingBackendKind.EcKeyboard);
    }

    [Theory]
    [InlineData("ANV16-41")]
    [InlineData("ANV16-42")]
    [InlineData("ANV16-A31")]
    public void Modes_are_offered_and_the_coolboost_stub_is_not(string model)
    {
        // Function 7 answers status 0 on these, but nothing acts on it; the modes work. CoolBoost doesn't come back when
        // the user turns them off either.
        var caps = CapabilityProbe.Probe(Device(new LightingFirmware(), model), model: model);
        var modesOff = new CapabilityOverrides { OperatingModes = false }.Apply(caps);

        Assert.False(caps.CoolBoost);
        Assert.False(modesOff.CoolBoost);
        Assert.NotEmpty(caps.OperatingModes);
    }

    [Fact]
    public void Reviewed_v16_sensor_mask_prevents_extra_queries_and_absent_system_fan()
    {
        var firmware = new LightingFirmware();
        var caps = CapabilityProbe.Probe(Device(firmware, "ANV16-42"), model: "ANV16-42");

        Assert.DoesNotContain(firmware.Base.Calls, c => c.Method == "GetGamingSysInfo" &&
            (c.Input == AcerProtocol.SensorReadInput(SensorId.Gpu2FanSpeed) || c.Input == AcerProtocol.SensorReadInput(SensorId.Gpu2Temperature)));
        Assert.DoesNotContain(SensorId.SystemFanSpeed, caps.Sensors);
    }

    [Fact]
    public async Task Brightness_only_write_preserves_existing_fields_and_selects_keyboard()
    {
        var firmware = new LightingFirmware();
        var before = firmware.Record.ToArray();
        var backend = new EcKeyboardBrightnessBackend(new ImmediateDispatcher(Device(firmware)), new() { WmiBacklightBrightness = true });

        Assert.True(await backend.ApplyAsync(new() { Effect = LightingEffect.Meteor, Brightness = 62, EffectColor = "#FFFFFF" }));

        Assert.Equal(50, firmware.Record[2]);
        Assert.Equal(1, firmware.Record[9]);
        Assert.Equal(before.Where((_, i) => i != 2).Take(8), firmware.Record.Where((_, i) => i != 2).Take(8));
        Assert.DoesNotContain(firmware.Base.Writes, c => c.Method is "SetGamingLED" or "SetGamingRgbKb");
    }

    [Fact]
    public async Task Brightness_fallback_preserves_current_auto_off_delay()
    {
        var firmware = new LightingFirmware { Timeout = (75, 90) };
        var backend = new EcKeyboardBrightnessBackend(new ImmediateDispatcher(Device(firmware)), new() { BacklightHotkey = 0x84 });

        Assert.True(await backend.ApplyAsync(new() { Brightness = 28 }));

        Assert.Equal((25, 90), firmware.Timeout);
    }

    [Fact]
    public async Task Unsupported_zone_switch_does_not_reject_working_rgb_writes()
    {
        var firmware = new LightingFirmware { RejectZones = true };
        var backend = new EcKeyboardBackend(new ImmediateDispatcher(Device(firmware)), Rgb with { ZoneSwitches = false });

        Assert.True(await backend.ApplyAsync(new() { Zones = [new(true, "#123456"), new(false, "#FFFFFF")] }));

        Assert.Equal(new RgbColor(0x12, 0x34, 0x56), firmware.Colors[1]);
        Assert.Equal(default, firmware.Colors[2]);
        Assert.DoesNotContain(firmware.Base.Writes, c => c.Method == "SetGamingLED");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task All_black_or_off_uses_brightness_zero_without_triggering_kbdf(bool on)
    {
        var firmware = new LightingFirmware();
        firmware.Colors[1] = new(10, 20, 30);
        var backend = new EcKeyboardBackend(new ImmediateDispatcher(Device(firmware)), Rgb with { ResetsBlackStaticColors = true });

        Assert.True(await backend.ApplyAsync(new() { Zones = Enumerable.Repeat(new ZoneSetting(on, "#000000"), 4).ToList() }));

        Assert.Equal(0, firmware.Record[2]);
        Assert.Equal(new RgbColor(10, 20, 30), firmware.Colors[1]);
        Assert.DoesNotContain(firmware.Base.Writes, c => c.Method is "SetGamingLED" or "SetGamingRgbKb");
    }

    [Fact]
    public async Task Dynamic_readback_reverses_channel_correction_and_preserves_black()
    {
        var firmware = new LightingFirmware();
        var backend = new EcKeyboardBackend(new ImmediateDispatcher(Device(firmware)), Rgb with { ColorAdjust = [0.5, 1, 0.5] });

        Assert.True(await backend.ApplyAsync(new() { Effect = LightingEffect.Breathing, EffectColor = "#804020" }));
        Assert.Equal("#804020", (await backend.ReadAsync())!.EffectColor);
        Assert.True(await backend.ApplyAsync(new() { Effect = LightingEffect.Breathing, EffectColor = "#000000" }));
        Assert.Equal("#000000", (await backend.ReadAsync())!.EffectColor);
    }

    [Fact]
    public async Task Unreadable_static_zone_mask_is_not_adopted_as_all_on_red()
    {
        var firmware = new LightingFirmware();
        firmware.Record[0] = 0;
        var backend = new EcKeyboardBackend(new ImmediateDispatcher(Device(firmware)), Rgb with { ZoneSwitchReadback = false });

        Assert.Null(await backend.ReadAsync());
    }

    [Theory]
    [InlineData("#ZZ1122")]
    [InlineData("malformed")]
    [InlineData(null)]
    public void Malformed_saved_colour_does_not_throw(string? color) => Assert.Equal(RgbColor.White, RgbColor.FromHex(color));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Usb_settings_are_advertised_independently(bool windowsKey, bool autoOff)
    {
        var channel = new SelectiveHid(new FakeUsbKeyboard(0x05AF, 0x668A), windowsKey, autoOff);
        var bus = new FakeHidBus();
        bus.Devices.Add(channel);
        using var lights = HidLights.Open(bus, null, _ => { }, _ => { });
        var caps = lights.Merge(DeviceCapabilities.None with { Keyboard = new() { BacklightHotkey = 0x84 } });

        Assert.Equal(windowsKey, caps.Keyboard.UsbWindowsKey);
        Assert.Equal(autoOff, caps.Keyboard.UsbBacklightTimeout);
        Assert.Equal(autoOff ? null : (byte?)0x84, caps.Keyboard.BacklightHotkey);
    }

    [Fact]
    public void An_opened_rgb_interface_replaces_the_wmi_brightness_fallback()
    {
        var channel = new FakeKyd100();
        channel.Lights[(byte)Kyd100Light.Keyboard] = (4, 0x0008);
        var bus = new FakeHidBus();
        bus.Devices.Add(channel);
        using var lights = HidLights.Open(bus, "ANV16-A31", _ => { }, _ => { });
        var caps = lights.Merge(DeviceCapabilities.None with { Lights = [EcKeyboardBrightnessBackend.Describe()] });

        Assert.DoesNotContain(caps.Lights, l => l.Backend == LightingBackendKind.EcKeyboardBrightness);
        Assert.Contains(caps.Lights, l => l.Backend == LightingBackendKind.Kyd100);
    }

    [Fact]
    public void Optional_hid_failure_keeps_wmi_capabilities_and_disposes_partial_lighting_open()
    {
        var ec = new SelectiveHid(new FakeEcHid()) { ThrowRead = true };
        var bus = new FakeHidBus();
        bus.Devices.Add(ec);
        using var hid = EcHidDevice.Open(bus, _ => { }, machineLock: false);
        var caps = CapabilityProbe.Probe(new AcerDevice(new FakeFirmware()) { EcHid = hid });
        Assert.True(caps.FirmwarePresent);
        Assert.Null(caps.EcHid);
        Assert.Contains("EC HID capability probe failed", caps.Diagnostics);

        var usb = new SelectiveHid(new FakeUsbKeyboard(0x05AF, 0x668A)) { ThrowRead = true };
        bus = new FakeHidBus();
        bus.Devices.Add(usb);
        Assert.Throws<InvalidOperationException>(() => HidLights.Open(bus, null, _ => { }, _ => { }));
        Assert.True(usb.Disposed);
    }

    private static AcerDevice Device(LightingFirmware firmware, string? model = null) => new(firmware)
    {
        FirmwareProfile = AcerFirmwareProfile.For(model),
    };

    private sealed class LightingFirmware : IWmiTransport
    {
        public FakeFirmware Base { get; } = new() { SupportsModes = true, SupportsCoolBoost = true };
        public byte[] Record { get; private set; } = [1, 5, 75, 0, 1, 0x12, 0x34, 0x56, 0x2A, 0, 0, 0, 0, 0, 0, 0];
        public Dictionary<int, RgbColor> Colors { get; } = [];
        public bool RejectZones { get; init; }
        public (int Brightness, int Seconds) Timeout { get; set; } = (75, 30);
        public bool IsClassAvailable(string className) => Base.IsClassAvailable(className);
        public IReadOnlyList<string> ReadStrings(string className, string propertyName) => Base.ReadStrings(className, propertyName);
        public bool HasArrayInput(string className, string method) => Base.HasArrayInput(className, method);

        public WmiArrayResult InvokeArray(string className, string method, byte[]? input)
        {
            if (method == "SetGamingKBBacklight")
                Record = input!.ToArray();
            return Base.InvokeArray(className, method, input);
        }

        public WmiOutputs InvokeNamed(string className, string method, IReadOnlyList<WmiArgument> inputs)
        {
            if (method == "GetGamingKBBacklight")
                return FakeFirmware.Outputs(("gmReturn", 0UL), ("gmOutput", Record.ToArray()));
            if (method == "GetGamingLED")
                return FakeFirmware.Outputs(("gmOutput", RejectZones ? 1UL : 15UL << 40));
            return Base.InvokeNamed(className, method, inputs);
        }

        public ulong Invoke(string className, string method, ulong input)
        {
            if (method == "GetFunction" && input == KeyboardProtocol.BacklightTimeoutQuery(0x84))
                return (ulong)Timeout.Brightness << 32 | (ulong)Timeout.Seconds << 40;
            if (method == "SetFunction" && (input & 0xFFFFFF) == (KeyboardProtocol.BacklightTimeoutInput(0x84, 0, 0) & 0xFFFFFF))
            {
                Timeout = (KeyboardProtocol.BacklightBrightnessValue(input), KeyboardProtocol.BacklightTimeoutValue(input));
                return 0;
            }
            if (method == "GetGamingRgbKb")
                return Colors.TryGetValue(Zone(input), out var color) ? KeyboardProtocol.ZoneColorInput(1, color) & ~0xFFUL : 1;
            if (method == "SetGamingRgbKb")
            {
                Colors[Zone(input & 0xFF)] = KeyboardProtocol.ZoneColorValue(input);
                Base.Invoke(className, method, input);
                return 0;
            }
            if (method == "SetGamingLED" && RejectZones)
                throw new InvalidOperationException("Unsupported zone setter must not be called.");
            return Base.Invoke(className, method, input);
        }

        private static int Zone(ulong mask) => Enumerable.Range(1, 4).First(i => (mask & (1UL << (i - 1))) != 0);
        public void Dispose() => Base.Dispose();
    }

    private sealed class SelectiveHid(IHidDevice inner, bool windowsKey = true, bool autoOff = true) : IHidDevice
    {
        private byte _command;
        public HidDeviceInfo Info => inner.Info;
        public bool ThrowRead { get; init; }
        public bool Disposed { get; private set; }
        public bool SetFeature(ReadOnlySpan<byte> report)
        {
            _command = report[1];
            return inner.SetFeature(report);
        }
        public bool GetFeature(Span<byte> report)
        {
            if (ThrowRead)
                throw new InvalidOperationException("Optional HID probe failed.");
            return (_command != 0x83 || windowsKey) && (_command != 0xB0 || autoOff) && inner.GetFeature(report);
        }
        public bool Write(ReadOnlySpan<byte> report) => inner.Write(report);
        public void Dispose()
        {
            Disposed = true;
            inner.Dispose();
        }
    }
}
