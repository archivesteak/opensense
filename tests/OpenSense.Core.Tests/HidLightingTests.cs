using Microsoft.Extensions.Logging.Abstractions;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Ipc;
using OpenSense.Core.Lighting;

namespace OpenSense.Core.Tests;

public sealed class UsbKeyboardProtocolTests
{
    private static readonly RgbColor Color = new(0x01, 0x02, 0x03);

    private static string Hex(IEnumerable<byte[]> reports) => string.Join(" ", reports.Select(Convert.ToHexString));

    [Fact]
    public void Commands_carry_report_id_zero_and_a_checksum()
    {
        Assert.Equal("0008020305190102D1", Convert.ToHexString(UsbKeyboardProtocol.Command(0x08, 0x02, 0x03, 0x05, 0x19, 0x01, 0x02)));
        Assert.Equal("00B10000000000004E", Convert.ToHexString(UsbKeyboardProtocol.Prepare()));
        Assert.Equal("0003030000000000F9", Convert.ToHexString(UsbKeyboardProtocol.WindowsKeys(locked: true)));
        Assert.Equal(25, UsbKeyboardProtocol.BrightnessByte(50));
        Assert.Equal(1, UsbKeyboardProtocol.SpeedByte(9));
        Assert.Equal(9, UsbKeyboardProtocol.SpeedByte(1));
    }

    [Fact]
    public void Chicony_effect_is_prepare_colour_effect()
    {
        var model = UsbKeyboardProtocol.Find(0x04F2, 0x0117)!;

        var sequence = UsbKeyboardProtocol.EffectSequence(model, UsbKeyboardEffect.Wave, 5, 50, UsbKeyboardProtocol.DirectionLeft, Color, random: true);

        // Chicony takes no random colours: c is always 1.
        Assert.Equal("00B10000000000004E 0014000001020300E5 0008020305190103D0", Hex(sequence));
    }

    [Fact]
    public void Sunrex_effect_is_prepare_clear_effect_colour_and_the_2023_keyboards_take_it_twice()
    {
        var older = UsbKeyboardProtocol.Find(0x05AF, 0x766C)!;
        var newer = UsbKeyboardProtocol.Find(0x05AF, 0x668A)!;

        var breathing = UsbKeyboardProtocol.EffectSequence(older, UsbKeyboardEffect.Breathing, 5, 100, 1, Color, random: true);
        var wave = UsbKeyboardProtocol.EffectSequence(newer, UsbKeyboardEffect.Wave, 5, 100, UsbKeyboardProtocol.DirectionRight, Color, random: false);

        Assert.Equal(8, breathing.Count);
        Assert.Equal(Hex(breathing.Take(4)), Hex(breathing.Skip(4)));
        Assert.Equal("B1", Convert.ToHexString(breathing[0])[2..4]);
        Assert.Equal("0802", Convert.ToHexString(breathing[1])[2..6]);
        Assert.Equal("08020205" + "32" + "E0" + "00", Convert.ToHexString(breathing[2])[2..16]); // random colours, no direction
        Assert.Equal("14000001020300", Convert.ToHexString(breathing[3])[2..16]);
        Assert.Equal(4, wave.Count);
        Assert.Equal("08020305320002", Convert.ToHexString(wave[2])[2..16]); // Wave's direction + 1
    }

    [Fact]
    public void MagKey_effects_use_colour_slot_one_and_slash_goes_out_as_4C()
    {
        var model = UsbKeyboardProtocol.Find(0x05AF, 0x667A)!;

        var slash = UsbKeyboardProtocol.MagKeySequence(model, MagKeyEffect.Slash, 5, 100, 1, Color, false);
        var fixedColor = UsbKeyboardProtocol.MagKeySequence(model, MagKeyEffect.Static, 5, 100, 1, Color, false);

        Assert.Equal((byte)0x4C, slash[2][3]);
        Assert.Equal((byte)0x00, slash[3][3]);
        Assert.Equal((byte)0x41, fixedColor[2][3]);
        Assert.Equal((byte)0x01, fixedColor[3][3]);
    }

    [Fact]
    public void Per_key_data_puts_each_led_at_four_n_plus_one()
    {
        var sunrex = UsbKeyboardProtocol.Find(0x05AF, 0x666A)!;
        var chicony = UsbKeyboardProtocol.Find(0x04F2, 0x0117)!;
        var leds = new Dictionary<int, RgbColor> { [0] = new(0xFF, 0, 0), [125] = Color, [126] = Color, [127] = Color };

        var data = UsbKeyboardProtocol.UploadData(sunrex, leds);
        var chiconyData = UsbKeyboardProtocol.UploadData(chicony, leds);

        Assert.Equal(8, data.Count);
        Assert.All(data, r => Assert.Equal(65, r.Length));
        Assert.Equal("0000FF0000", Convert.ToHexString(data[0].AsSpan(0, 5)));
        Assert.Equal("010203", Convert.ToHexString(data[7].AsSpan(1 + 501 - 448, 3)));
        Assert.All(data[7].Skip(1 + 504 - 448), b => Assert.Equal(0, b)); // Sunrex carries 126 LEDs
        Assert.Equal("010203", Convert.ToHexString(chiconyData[7].AsSpan(1 + 509 - 448, 3)));
        Assert.Equal("00000000FF000000FF000000FF", Convert.ToHexString(UsbKeyboardProtocol.MagKeyData([new(0, 0, 0xFF)]).AsSpan(0, 13)));
        Assert.Equal(49, UsbKeyboardProtocol.MagKeyData([]).Length);
    }

    [Fact]
    public void Keyboards_and_their_layouts_follow_Acers_type_table()
    {
        Assert.Equal(KeyboardLayout.Jis, UsbKeyboardProtocol.Find(0x04F2, 0x0119)!.Layout);
        Assert.Equal(KeyboardLayout.Iso, UsbKeyboardProtocol.Find(0x05AF, 0x766A)!.Layout); // the 766s' letters differ
        Assert.Equal(KeyboardLayout.Ansi, UsbKeyboardProtocol.Find(0x05AF, 0x766C)!.Layout);
        Assert.Equal(KeyboardLayout.Ansi, UsbKeyboardProtocol.Find(0x05AF, 0x666A)!.Layout);
        Assert.Equal(KeyboardLayout.Iso, UsbKeyboardProtocol.Find(0x05AF, 0x868E)!.Layout);
        Assert.Equal(UsbKeyboardGeneration.Sunrex2025, UsbKeyboardProtocol.Find(0x05AF, 0x767B)!.Generation);
        Assert.False(UsbKeyboardProtocol.Find(0x0D62, 0x0ABD)!.Lighting);
        Assert.Null(UsbKeyboardProtocol.Find(0x0D62, 0x0ABD)!.Layout);
        Assert.Null(UsbKeyboardProtocol.Find(0x05AF, 0x669A));
        Assert.Null(UsbKeyboardProtocol.Find(0x04F2, 0x1516));
    }

    [Fact]
    public void Every_known_keyboard_has_its_keys_once()
    {
        foreach (var product in new ushort[] { 0x0117, 0x011A, 0x0119 })
            Assert.NotNull(UsbKeyboardLeds.For(UsbKeyboardProtocol.ChiconyVendor, product));
        foreach (var family in new[] { 0x666, 0x667, 0x668, 0x766, 0x767, 0x866, 0x867, 0x868 })
        {
            foreach (var letter in new[] { 0xA, 0xB, 0xC, 0xD, 0xE })
            {
                var leds = UsbKeyboardLeds.For(UsbKeyboardProtocol.SunrexVendor, (ushort)(family << 4 | letter));
                if (family == 0x767 && letter == 0xC)
                    Assert.Null(leds); // Acer's software has no table for it either
                else
                    Assert.True(leds is { Count: > 80 }, $"{family:X3}{letter:X}");
            }
        }

        var us = UsbKeyboardLeds.For(0x04F2, 0x0117)!;
        var uk = UsbKeyboardLeds.For(0x04F2, 0x011A)!;
        var sunrex2026 = UsbKeyboardLeds.For(0x05AF, 0x668C)!;
        Assert.Equal(13, us["Backslash"]);
        Assert.Equal(13, uk["IntlBackslash"]);
        Assert.Equal(87, uk["IntlHash"]);
        Assert.Equal(53, sunrex2026["Convert"]);
        Assert.Equal(73, sunrex2026["Equal"]);
        Assert.Equal(67, sunrex2026["Minus"]);
        Assert.False(UsbKeyboardLeds.For(0x05AF, 0x666C)!.ContainsKey("Convert")); // its LED is past the upload
    }
}

public sealed class Kyd100ProtocolTests
{
    [Fact]
    public void Update_packs_values_as_wide_as_the_descriptor_says()
    {
        var report = Kyd100Protocol.Update(FakeKyd100.Descriptor, Kyd100Light.Keyboard,
            new Kyd100Update(Kyd100Mode.Breathing, 100, 3, 0, new RgbColor(0x10, 0x20, 0x30), 0x0102));

        Assert.Equal(FakeKyd100.ReportLength, report.Length);
        Assert.Equal("A4210464030010203002010000", Convert.ToHexString(report.AsSpan(0, 13)));
    }

    [Fact]
    public void A_value_the_descriptor_lacks_is_left_out()
    {
        var descriptor = FakeKyd100.Descriptor with
        {
            FeatureValues = [.. FakeKyd100.Descriptor.FeatureValues.Where(v => v.Usage != 0x44)],
        };

        var report = Kyd100Protocol.Update(descriptor, Kyd100Light.Logo, new Kyd100Update(Kyd100Mode.Static, 50, 0, 4, new RgbColor(1, 2, 3), 1));

        Assert.Equal("A480023200010203010000", Convert.ToHexString(report.AsSpan(0, 11)));
    }

    [Fact]
    public void Light_list_and_info_decode()
    {
        Assert.Equal([Kyd100Light.LightBar, Kyd100Light.Keyboard], Kyd100Protocol.DecodeList([0xA1, 3, 0x20, 0x99, 0x21]));
        byte[] info = [0xA3, 0x21, 9, 9, 16, 9, 0x08, 0x10, 0, 0];
        Assert.Equal(new Kyd100LightInfo(Kyd100Light.Keyboard, 16, 0x1008), Kyd100Protocol.DecodeInfo(FakeKyd100.Descriptor, info, Kyd100Light.Keyboard));
        Assert.Null(Kyd100Protocol.DecodeInfo(FakeKyd100.Descriptor, info, Kyd100Light.Logo)); // the answer names another light
    }

    [Fact]
    public void Each_kind_of_light_offers_its_modes_the_interface_reports()
    {
        Assert.Equal([Kyd100Mode.Breathing, Kyd100Mode.Ripple], Kyd100Protocol.Modes(Kyd100Light.Keyboard, 0x1008));
        Assert.Equal([Kyd100Mode.Breathing, Kyd100Mode.Neon, Kyd100Mode.Wave, Kyd100Mode.Twinkling, Kyd100Mode.FollowOperatingMode],
            Kyd100Protocol.Modes(Kyd100Light.RearLightBar, 0x7FF8));
        Assert.Equal([Kyd100Mode.Breathing, Kyd100Mode.Neon, Kyd100Mode.FollowOperatingMode], Kyd100Protocol.Modes(Kyd100Light.CoverLogo, 0x7FF8));
    }
}

public sealed class DarfonProtocolTests
{
    [Fact]
    public void Effect_is_colour_then_effect()
    {
        var sequence = DarfonProtocol.EffectSequence(DarfonPart.LightBar, DarfonEffect.Snake, 3, 80, 1, new RgbColor(1, 2, 3), random: false);

        Assert.Equal("00145705010203", Convert.ToHexString(sequence[0])[..14]);
        Assert.Equal("00080557055000", Convert.ToHexString(sequence[1])[..14]);
        Assert.Equal((byte)0x02, sequence[1][7]);
        Assert.Equal((byte)0x08, DarfonProtocol.EffectSequence(DarfonPart.CoverLogo, DarfonEffect.Breathing, 1, 100, 1, default, true)[1][6]);
    }

    [Fact]
    public void Static_colours_of_the_logo_and_the_bar()
    {
        var logo = DarfonProtocol.LogoStaticSequence(100, [new(1, 1, 1), new(2, 2, 2)]);
        var bar = DarfonProtocol.BarStaticSteps(DarfonPart.InfiniteRing, 60, new RgbColor(9, 8, 7));

        Assert.Equal(4, logo.Count);
        Assert.Equal("00080101056401", Convert.ToHexString(logo[0])[..14]);
        Assert.Equal("00140103020202", Convert.ToHexString(logo[3])[..14]); // the last LED takes the last colour
        Assert.Equal(3, bar.Count);
        Assert.Equal("003A", Convert.ToHexString(bar[1][0])[..4]);
        Assert.Equal("00140000090807", Convert.ToHexString(bar[2][0])[..14]);
        Assert.Equal("00410100", Convert.ToHexString(DarfonProtocol.Hello())[..8]);
    }
}

/// <summary>The HID and USB lights behind fakes: detection, precedence over the embedded controller, what goes out.</summary>
public sealed class HidLightsTests
{
    private static readonly List<string> Log = [];

    private static HidLights Open(FakeHidBus bus, string? model = "Predator PHN16-72") => HidLights.Open(bus, model, Log.Add, _ => { });

    private static DeviceCapabilities EcLights() => DeviceCapabilities.None with
    {
        Lights =
        [
            EcKeyboardBackend.Describe(new KeyboardCapabilities { RgbBacklight = true, Zones = 4, Effects = KeyboardProtocol.ZonedEffects }),
            EcLightBarBackend.Describe([new LightBar(LightBarId.Front, 2)], 12),
            EcLogoBackend.Describe(),
        ],
        Keyboard = new KeyboardCapabilities { BacklightHotkey = 0x84, WindowsKey = true },
    };

    [Fact]
    public void Lighting_interface_keyboard_replaces_the_ec_keyboard_and_its_other_lights_the_bars_and_logo()
    {
        var kyd = new FakeKyd100();
        kyd.Lights[0x21] = (4, 0x7FF8);
        kyd.Lights[0x83] = (1, 0x0038);
        var bus = new FakeHidBus();
        bus.Devices.Add(kyd);

        using var lights = Open(bus);
        var caps = lights.Merge(EcLights());

        Assert.Equal(["Keyboard", "CoverLogo"], caps.Lights.Select(l => l.Id));
        Assert.All(caps.Lights, l => Assert.Equal(LightingBackendKind.Kyd100, l.Backend));
        Assert.Equal(4, caps.Lights[0].Zones);
        Assert.Equal(9, caps.Lights[0].Traits(LightingEffect.Wave)!.MaxSpeed);
        Assert.Equal(5, caps.Lights[1].Traits(LightingEffect.Breathing)!.MaxSpeed);
    }

    [Fact]
    public void Lighting_interface_with_only_a_keyboard_leaves_the_ec_bars_and_logo()
    {
        var kyd = new FakeKyd100();
        kyd.Lights[0x21] = (4, 0x0008);
        var bus = new FakeHidBus();
        bus.Devices.Add(kyd);

        using var lights = Open(bus);
        var caps = lights.Merge(EcLights());

        Assert.Equal([LightingBackendKind.EcLightBar, LightingBackendKind.EcLogo, LightingBackendKind.Kyd100], caps.Lights.Select(l => l.Backend));
    }

    [Fact]
    public async Task Lighting_interface_static_colours_go_led_by_led()
    {
        var kyd = new FakeKyd100();
        kyd.Lights[0x21] = (4, 0x0008);
        var bus = new FakeHidBus();
        bus.Devices.Add(kyd);
        using var lights = Open(bus);
        using var worker = new LightingWorker();
        var backend = Assert.Single(lights.CreateBackends(worker, lights.Lights));

        var ok = await backend.ApplyAsync(new LightingSettings
        {
            Brightness = 75,
            Zones = [new(true, "#FF0000"), new(false, "#00FF00"), new(true, "#0000FF"), new(true, "#FFFFFF")],
        });
        var breathing = await backend.ApplyAsync(new LightingSettings { Effect = LightingEffect.Breathing, Speed = 4, EffectColor = "#102030" });

        Assert.True(ok && breathing);
        Assert.Equal(["2102", "2102", "2102", "2102"], kyd.Updates.Take(4).Select(u => Convert.ToHexString(u.AsSpan(0, 2))));
        Assert.Equal(["0100", "0200", "0400", "0800"], kyd.Updates.Take(4).Select(u => Convert.ToHexString(u.AsSpan(8, 2))));
        Assert.Equal("000000", Convert.ToHexString(kyd.Updates[1].AsSpan(5, 3))); // a zone switched off is dark
        Assert.Equal("21046404001020300000", Convert.ToHexString(kyd.Updates[4]));
    }

    [Fact]
    public async Task Per_key_keyboard_uploads_its_colours_and_takes_the_keyboard_settings()
    {
        var keyboard = new FakeUsbKeyboard(0x05AF, 0x668A);
        var bus = new FakeHidBus();
        bus.Devices.Add(keyboard);
        using var lights = Open(bus);
        var caps = lights.Merge(EcLights());
        using var worker = new LightingWorker();
        var light = Assert.Single(caps.Lights, l => l.Location == LightingLocation.Keyboard);
        var backend = Assert.Single(lights.CreateBackends(worker, [light]));

        var ok = await backend.ApplyAsync(new LightingSettings
        {
            Effect = LightingEffect.PerKey,
            Brightness = 100,
            KeyColors = new Dictionary<string, string> { ["KeyW"] = "#112233", ["Escape"] = "#FFFFFF" },
        });

        Assert.True(ok);
        Assert.Equal(LightingBackendKind.UsbKeyboard, light.Backend);
        Assert.Equal(KeyboardLayout.Ansi, light.Layout);
        Assert.Contains("NumpadEnter", light.Keys);
        Assert.Equal(LightingEffect.PerKey, light.Effects[0].Effect);
        Assert.True(light.Traits(LightingEffect.Breathing)!.RandomColor);
        Assert.DoesNotContain(caps.Lights, l => l.Backend == LightingBackendKind.EcKeyboard);
        Assert.Equal(8, keyboard.Data.Count);
        Assert.Equal("112233", Convert.ToHexString(keyboard.Data[0].AsSpan(4 * 14 + 1, 3))); // W is LED 14
        Assert.Equal("FFFFFF", Convert.ToHexString(keyboard.Data[0].AsSpan(1, 3)));
        Assert.Equal("12000008", Convert.ToHexString(keyboard.Commands[^2].AsSpan(0, 4)));
        Assert.Equal("08023305320801", Convert.ToHexString(keyboard.Commands[^1].AsSpan(0, 7)));
        Assert.Equal(0, keyboard.BadChecksums);

        Assert.True(caps.Keyboard.UsbWindowsKey);
        Assert.True(caps.Keyboard.UsbBacklightTimeout);
        Assert.Null(caps.Keyboard.BacklightHotkey);
        var service = new KeyboardService(new ImmediateDispatcher(new AcerDevice(new FakeFirmware())), caps.Keyboard, lights.Keyboard);
        await service.ApplyAsync(new KeyboardSettings { WindowsKey = false, BacklightAutoOff = true });
        Assert.True(keyboard.WindowsKeyLocked);
        Assert.True(keyboard.AutoOff);
        var state = await service.ReadStateAsync();
        Assert.False(state.WindowsKey);
        Assert.True(state.BacklightAutoOff);
    }

    [Fact]
    public async Task MagForce_keys_come_with_the_models_that_have_them_or_when_turned_on()
    {
        var keyboard = new FakeUsbKeyboard(0x05AF, 0x667A);
        var bus = new FakeHidBus();
        bus.Devices.Add(keyboard);

        using (var helios = Open(bus, "Predator PH16-73"))
        {
            var mag = Assert.Single(helios.Merge(EcLights()).Lights, l => l.Location == LightingLocation.MagKey);
            Assert.Equal(UsbKeyboardProtocol.MagKeys, mag.Keys);
            Assert.NotNull(mag.Traits(LightingEffect.Swiping)); // the 2025 list
            using var worker = new LightingWorker();
            var backend = Assert.Single(helios.CreateBackends(worker, [mag]));
            Assert.True(await backend.ApplyAsync(new LightingSettings
            {
                Effect = LightingEffect.PerKey,
                KeyColors = new Dictionary<string, string> { ["KeyD"] = "#0000FF" },
            }));
            var data = Assert.Single(keyboard.Data);
            Assert.Equal("000000FF000000FF000000FF", Convert.ToHexString(data.AsSpan(36, 12)));
            Assert.Equal("13000008", Convert.ToHexString(keyboard.Commands[^2].AsSpan(0, 4)));
            Assert.Equal("08024F05", Convert.ToHexString(keyboard.Commands[^1].AsSpan(0, 4)));
        }

        using var other = Open(bus, "Predator PHN16-72");
        var caps = other.Merge(EcLights());
        Assert.DoesNotContain(caps.Lights, l => l.Location == LightingLocation.MagKey);
        Assert.NotNull(caps.MagKeyLight);
        Assert.Contains(new CapabilityOverrides { MagKey = true }.Apply(caps).Lights, l => l.Location == LightingLocation.MagKey);
    }

    [Fact]
    public async Task Darfon_lights_are_greeted_and_get_their_own_ids()
    {
        var darfon = new FakeHidDevice(new HidDeviceInfo(@"\\?\hid#vid_0d62&pid_a00a#simulated", 0x0D62, 0xA00A, 1, DarfonProtocol.UsagePage, 1, 0, 65, 9));
        var bus = new FakeHidBus();
        bus.Devices.Add(darfon);
        using var lights = Open(bus);
        var caps = lights.Merge(EcLights());
        using var worker = new LightingWorker();

        Assert.Equal("00410100", Convert.ToHexString(darfon.Writes[0].AsSpan(0, 4)));
        Assert.Equal(["Keyboard", "LightBar", "Logo", "CoverLogo", "LightBar2"], caps.Lights.Select(l => l.Id));
        var bar = caps.Lights[^1];
        Assert.NotNull(bar.Traits(LightingEffect.RowWave));
        Assert.NotNull(caps.Lights[3].Traits(LightingEffect.Swiping)); // a 2025 logo
        var backend = Assert.Single(lights.CreateBackends(worker, [bar]));
        Assert.True(await backend.ApplyAsync(new LightingSettings { Effect = LightingEffect.Racing, Speed = 5, Brightness = 100 }));
        Assert.Equal("0014", Convert.ToHexString(darfon.Features[^2].AsSpan(0, 2)));
        Assert.Equal("0008055E0164", Convert.ToHexString(darfon.Features[^1].AsSpan(0, 6)));
    }
}

/// <summary>A 2024 Predator with HID and USB lights, the engine behind the real JSON-RPC stack.</summary>
public sealed class HidLightingEngineTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeKyd100 _kyd = new();
    private readonly FakeUsbKeyboard _keyboard = new(0x05AF, 0x668A);
    private readonly FakeHidDevice _darfon = new(new HidDeviceInfo(@"\\?\hid#vid_0d62&pid_ba51#simulated", 0x0D62, 0xBA51, 1, DarfonProtocol.UsagePage, 1, 0, 65, 9));
    private SimulatedMachine _machine = null!;
    private OpenSenseEngine _engine = null!;
    private StreamJsonRpc.JsonRpc _server = null!;
    private StreamJsonRpc.JsonRpc _client = null!;
    private IOpenSenseService _service = null!;

    public ValueTask InitializeAsync()
    {
        _kyd.Lights[0x20] = (1, 0x0478);
        _machine = new SimulatedMachine(SimulatedModel.Predator2024);
        _machine.Hid.Devices.AddRange([_kyd, _keyboard, _darfon]);
        _engine = new OpenSenseEngine(_machine, Path.Combine(_directory, "settings.json"), NullLogger<OpenSenseEngine>.Instance);
        _engine.Start();
        (_server, _client, _service) = IpcTests.Serve(_engine);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Lights_cross_the_wire_and_per_key_colours_reach_the_keyboard()
    {
        var snapshot = await _service.GetSnapshotAsync(TestContext.Current.CancellationToken);

        // The embedded controller's keyboard, light bars and logo give way.
        Assert.Equal(["LightBar", "Keyboard", "CoverLogo"], snapshot.Capabilities.Lights.Select(l => l.Id));
        var keyboard = snapshot.Capabilities.Lights[1];
        Assert.Equal(LightingBackendKind.UsbKeyboard, keyboard.Backend);
        Assert.Equal(KeyboardLayout.Ansi, keyboard.Layout);
        Assert.Contains("IntlHash", keyboard.Keys);
        Assert.Equal(LightingBackendKind.Kyd100, snapshot.Capabilities.Lights[0].Backend);
        Assert.NotNull(snapshot.Capabilities.Lights[2].Traits(LightingEffect.Neon)); // a 2024 logo
        Assert.NotNull(snapshot.Capabilities.MagKeyLight);
        Assert.True(snapshot.Capabilities.Keyboard.UsbWindowsKey);
        Assert.Contains("USB keyboard: 05AF:668A", snapshot.Detected.Diagnostics, StringComparison.Ordinal);

        var settings = new LightingSettings
        {
            Effect = LightingEffect.PerKey,
            RandomColor = true,
            Layout = KeyboardLayout.Iso,
            KeyColors = new Dictionary<string, string> { ["KeyA"] = "#ABCDEF" },
        };
        await _service.SetLightingAsync(new LightingConfig().With(keyboard.Id, settings), TestContext.Current.CancellationToken);

        Assert.Equal("ABCDEF", Convert.ToHexString(_keyboard.Data[0].AsSpan(4 * 9 + 1, 3))); // A is LED 9
        var stored = (await _service.GetSnapshotAsync(TestContext.Current.CancellationToken)).Settings.Lighting.For(keyboard.Id)!;
        Assert.True(stored.SameAs(settings));
        Assert.False(stored.SameAs(settings with { KeyColors = new Dictionary<string, string> { ["KeyA"] = "#000000" } }));
    }

    [Fact]
    public async Task MagKey_override_rebuilds_with_the_light()
    {
        var rebuilt = new TaskCompletionSource<EngineSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Rebuilt += (_, s) => rebuilt.TrySetResult(s);

        await _service.SetOverridesAsync(new CapabilityOverrides { MagKey = true }, TestContext.Current.CancellationToken);
        var snapshot = await rebuilt.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Contains(snapshot.Capabilities.Lights, l => l.Location == LightingLocation.MagKey);
        Assert.DoesNotContain(snapshot.Detected.Lights, l => l.Location == LightingLocation.MagKey);
        Assert.True(snapshot.Settings.Overrides.MagKey);
    }

    [Fact]
    public async Task Devices_that_leave_are_rescanned_and_clients_told()
    {
        var rebuilt = new TaskCompletionSource<EngineSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Rebuilt += (_, s) => rebuilt.TrySetResult(s);

        _machine.Hid.Remove(_darfon);
        var snapshot = await rebuilt.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal(["LightBar", "Keyboard"], snapshot.Capabilities.Lights.Select(l => l.Id));
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
