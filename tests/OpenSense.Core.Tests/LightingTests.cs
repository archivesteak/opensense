using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OpenSense.Core.Control;
using OpenSense.Core.Engine;
using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Boot;
using OpenSense.Core.Hardware.Hid;
using OpenSense.Core.Ipc;
using OpenSense.Core.Lighting;
using OpenSense.Core.Monitoring;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Tests;

public class LightingServiceTests
{
    private static readonly KeyboardCapabilities Keyboard = new()
    {
        RgbBacklight = true,
        Zones = 4,
        Effects = KeyboardProtocol.ZonedEffects,
    };

    private static (LightingService Service, SimulatedTransport Laptop) Create(TimeProvider? time = null)
    {
        var laptop = new SimulatedTransport(SimulatedModel.Nitro2021);
        var dispatcher = new ImmediateDispatcher(new AcerDevice(laptop));
        return (new LightingService([new EcKeyboardBackend(dispatcher, Keyboard)], time), laptop);
    }

    private static LightingConfig KeyboardOnly(LightingSettings settings) => new LightingConfig().With(EcKeyboardBackend.Id, settings);

    [Fact]
    public void Keyboard_light_describes_the_ec_keyboard()
    {
        var device = EcKeyboardBackend.Describe(Keyboard);

        Assert.Equal("Keyboard", device.Id);
        Assert.Equal(4, device.Zones);
        Assert.True(device.ZoneSwitches);
        Assert.Equal(KeyboardProtocol.BrightnessLevels, device.BrightnessLevels);
        Assert.Equal(7, device.Effects.Count);
        Assert.False(device.Traits(LightingEffect.Neon)!.Color);
        Assert.Equal([LightingDirection.Left, LightingDirection.Right], device.Traits(LightingEffect.Shifting)!.Directions);
        Assert.Equal([LightingDirection.Left, LightingDirection.Right], device.Traits(LightingEffect.Wave)!.Directions);
        Assert.False(device.Traits(LightingEffect.Meteor)!.Direction);
        Assert.True(device.Offers(LightingEffect.Static));
    }

    [Fact]
    public async Task Static_colours_set_zones_brightness_and_colours()
    {
        var (service, laptop) = Create();

        await service.ApplyAsync(KeyboardOnly(new LightingSettings
        {
            Brightness = 50,
            Zones = [new(true, "#102030"), new(false, "#FFFFFF"), new(true, "#00FF00"), new(true, "#0000FF")],
        }));

        Assert.Equal(new RgbColor(0x10, 0x20, 0x30), laptop.ZoneColor(1));
        Assert.Null(laptop.ZoneColor(2));
        Assert.Equal(new RgbColor(0, 255, 0), laptop.ZoneColor(3));
        Assert.Equal((byte)KeyboardEffect.Static, laptop.Backlight[0]);
        Assert.Equal(50, laptop.Backlight[2]);
    }

    [Fact]
    public async Task Zone_colours_and_switches_are_read_from_the_firmware()
    {
        var (service, laptop) = Create();
        await service.ApplyAsync(KeyboardOnly(new LightingSettings
        {
            Zones = [new(true, "#102030"), new(false, "#FFFFFF"), new(true, "#00FF00"), new(true, "#0000FF")],
        }));

        // A new service knows nothing of what was sent: it reads what the firmware keeps.
        var fresh = new LightingService([new EcKeyboardBackend(new ImmediateDispatcher(new AcerDevice(laptop)), Keyboard)]);
        var zones = (await fresh.ReadAsync())[EcKeyboardBackend.Id].Zones;

        Assert.Equal([true, false, true, true], zones.Select(z => z.On));
        Assert.Equal(["#102030", "#00FF00", "#0000FF"], new[] { zones[0], zones[2], zones[3] }.Select(z => z.Color));
    }

    [Fact]
    public async Task Effect_round_trips_through_the_firmware()
    {
        var (service, _) = Create();

        await service.ApplyAsync(KeyboardOnly(new LightingSettings
        {
            Effect = LightingEffect.Shifting, Speed = 3, Brightness = 75, Direction = LightingDirection.Left, EffectColor = "#FF8000",
        }));
        var state = (await service.ReadAsync())[EcKeyboardBackend.Id];

        Assert.Equal(LightingEffect.Shifting, state.Effect);
        Assert.Equal(3, state.Speed);
        Assert.Equal(75, state.Brightness);
        Assert.Equal(LightingDirection.Left, state.Direction);
        Assert.Equal("#FF8000", state.EffectColor);
    }

    [Theory]
    [InlineData(LightingDirection.Right, 1)]
    [InlineData(LightingDirection.Left, 2)]
    public async Task Keyboard_directions_go_out_as_acers_software_numbers_them(LightingDirection direction, byte expected)
    {
        var (service, laptop) = Create();

        await service.ApplyAsync(KeyboardOnly(new LightingSettings { Effect = LightingEffect.Wave, Direction = direction }));

        Assert.Equal(expected, laptop.Backlight[4]);
    }

    [Fact]
    public async Task Lights_without_settings_are_left_alone()
    {
        var (service, laptop) = Create();
        var before = laptop.Backlight;

        await service.ApplyAsync(new LightingConfig());

        Assert.Equal(before, laptop.Backlight);
    }

    [Fact]
    public async Task Unchanged_settings_are_not_sent_again_until_reapplied()
    {
        var (service, laptop) = Create();
        var settings = new LightingSettings { Effect = LightingEffect.Breathing, EffectColor = "#00FF00" };
        await service.ApplyAsync(KeyboardOnly(settings));

        laptop.OverwriteBacklight(KeyboardProtocol.BacklightPayload(KeyboardEffect.Neon, 5, 100, KeyboardDirection.Right, default));
        await service.ApplyAsync(KeyboardOnly(settings with { Zones = [.. settings.Zones] }));
        Assert.Equal((byte)KeyboardEffect.Neon, laptop.Backlight[0]);

        await service.ReapplyAsync();
        Assert.Equal((byte)KeyboardEffect.Breathing, laptop.Backlight[0]);
    }

    [Fact]
    public async Task After_a_sleep_the_lights_go_out_again_once_Acers_agent_has_restored_its_own()
    {
        var time = new FakeTimeProvider();
        var (service, laptop) = Create(time);
        await service.ApplyAsync(KeyboardOnly(new LightingSettings { Effect = LightingEffect.Breathing, EffectColor = "#00FF00" }));

        var resumed = service.OnResume();
        laptop.OverwriteBacklight(KeyboardProtocol.BacklightPayload(KeyboardEffect.Neon, 5, 100, KeyboardDirection.Right, default));
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(resumed.IsCompleted); // not while the agent may still overwrite them

        time.Advance(TimeSpan.FromSeconds(1));
        await resumed;
        Assert.Equal((byte)KeyboardEffect.Breathing, laptop.Backlight[0]);
    }

    [Fact]
    public async Task An_effect_the_light_lacks_is_refused_with_a_notice()
    {
        var laptop = new SimulatedTransport(SimulatedModel.Nitro2021);
        var dispatcher = new ImmediateDispatcher(new AcerDevice(laptop));
        var service = new LightingService([new EcKeyboardBackend(dispatcher, Keyboard with { Effects = [KeyboardEffect.Breathing] })]);
        var notices = new List<ControlNotice>();
        service.Notice += notices.Add;

        await service.ApplyAsync(KeyboardOnly(new LightingSettings { Effect = LightingEffect.Wave }));

        var notice = Assert.Single(notices);
        Assert.Equal(NoticeKind.LightingRejected, notice.Kind);
        Assert.Equal(LightingLocation.Keyboard, notice.Light);
    }
}

public class LightBarProtocolTests
{
    [Fact]
    public void Layout_counts_zone_fields_that_are_not_11()
    {
        // 16-byte form (interface 2.91): no front/left/right bar, a rear bar of seven zones (the eighth field reads 11).
        var wide = Convert.FromHexString("0000000000FFFFFFFFFFFF55D50000");
        Assert.Equal([new LightBar(LightBarId.Rear, 7)], LightBarProtocol.DecodeLayout(wide, wide: true));

        // 11-byte form: a two-zone front bar (F5: zones 3-4 absent), no left bar, an all-zero bar counts as absent.
        var narrow = Convert.FromHexString("0000000000F5FF0000");
        Assert.Equal([new LightBar(LightBarId.Front, 2)], LightBarProtocol.DecodeLayout(narrow, wide: false));
    }

    [Fact]
    public void On_off_addresses_one_bar_and_leaves_the_others_alone()
    {
        Assert.Equal(Convert.FromHexString("100000000000FFFFFF150000"),
            LightBarProtocol.OnOffArray(LightBarId.Rear, [true, true, true], 12));
        Assert.Equal(Convert.FromHexString("100000000000FFFFFFFFFFFF15000000"),
            LightBarProtocol.OnOffArray(LightBarId.Rear, [true, true, true], 16));
        // Eight zones need the second byte; dark zones are 00.
        Assert.Equal(Convert.FromHexString("100000000000FFFFFFFFFFFF11400000"),
            LightBarProtocol.OnOffArray(LightBarId.Rear, [true, false, true, false, false, false, false, true], 16));
    }

    [Fact]
    public void Zone_colour_carries_the_bar_and_zone()
    {
        Assert.Equal(0x0000_0208_8000_FF00UL, LightBarProtocol.ZoneColorInput(LightBarId.Rear, 2, new RgbColor(0xFF, 0x00, 0x80)));
    }

    [Fact]
    public void Effect_payload_is_predatorsense_layout_for_device_2()
    {
        Assert.Equal(Convert.FromHexString("01034B000100AEC70302000000000000"),
            LightBarProtocol.BacklightPayload(LightBarEffect.Breathing, 3, 75, 1, new RgbColor(0x00, 0xAE, 0xC7)));
        // Static carries only the brightness; Wave its direction and no colour; speed stays within 1-5.
        Assert.Equal(Convert.FromHexString("00003200000000000302000000000000"),
            LightBarProtocol.BacklightPayload(LightBarEffect.Static, 3, 50, 1, new RgbColor(1, 2, 3)));
        Assert.Equal(Convert.FromHexString("03056400020000000302000000000000"),
            LightBarProtocol.BacklightPayload(LightBarEffect.Wave, 9, 100, LightBarProtocol.DirectionLeft, new RgbColor(1, 2, 3)));
    }

    [Fact]
    public void Logo_colour_then_behaviour()
    {
        Assert.Equal(0x0003_4BC7_AE00_01UL, LogoProtocol.ColorInput(new RgbColor(0x00, 0xAE, 0xC7), 75, 3));
        Assert.Equal(0x0001_0001UL, LogoProtocol.BehaviorInput(LogoBehavior.Static));
        Assert.Equal(0x0003_0001UL, LogoProtocol.BehaviorInput(LogoBehavior.Breathing));
        Assert.Equal(0x0004_0001UL, LogoProtocol.BehaviorInput(LogoBehavior.Neon));
    }
}

public class LightBarBackendTests
{
    private static (LightingService Service, SimulatedTransport Laptop, LightingDeviceInfo Mirror, LightingDeviceInfo Logo) Create()
    {
        var laptop = new SimulatedTransport(SimulatedModel.Predator2024);
        var device = new AcerDevice(laptop);
        var dispatcher = new ImmediateDispatcher(device);
        var bars = device.GetLightBars(wide: true)!;
        var mirror = new EcLightBarBackend(dispatcher, bars, 16);
        var logo = new EcLogoBackend(dispatcher);
        return (new LightingService([mirror, logo]), laptop, mirror.Device, logo.Device);
    }

    [Fact]
    public void A_rear_bar_of_seven_zones_is_the_infinity_mirror()
    {
        var (_, _, mirror, _) = Create();

        Assert.Equal(LightingLocation.InfinityMirror, mirror.Location);
        Assert.Equal(7, mirror.Zones);
        Assert.True(mirror.ZoneSwitches);
        Assert.Equal([LightingEffect.Breathing, LightingEffect.Wave, LightingEffect.Neon, LightingEffect.Snake, LightingEffect.Lightning,
            LightingEffect.Stack, LightingEffect.MotionPoint, LightingEffect.ZoomIn], mirror.Effects.Select(e => e.Effect));
        Assert.All(mirror.Effects, e => Assert.Equal(5, e.MaxSpeed));
        Assert.Equal([LightingDirection.Left, LightingDirection.Right], mirror.Traits(LightingEffect.Wave)!.Directions);
    }

    [Fact]
    public void An_ordinary_bar_has_twinkling_but_not_the_mirrors_effects()
    {
        var front = EcLightBarBackend.Describe([new LightBar(LightBarId.Front, 2)], 12);

        Assert.Equal(LightingLocation.LightBar, front.Location);
        Assert.Equal([LightingEffect.Breathing, LightingEffect.Wave, LightingEffect.Neon, LightingEffect.Twinkling], front.Effects.Select(e => e.Effect));
        Assert.True(front.Traits(LightingEffect.Twinkling)!.Color);
        Assert.False(front.Traits(LightingEffect.Neon)!.Color);
        // Before interface 2.86 Acer's software sends only a fixed on/off value, so zones can't be switched.
        Assert.False(EcLightBarBackend.Describe([new LightBar(LightBarId.Front, 2)], 8).ZoneSwitches);
    }

    [Fact]
    public async Task Static_colours_go_zone_by_zone_and_leave_the_keyboard_alone()
    {
        var (service, laptop, mirror, _) = Create();
        var keyboardBefore = laptop.Backlight;
        var zones = Enumerable.Range(0, 7).Select(i => new ZoneSetting(i != 1, $"#0000{i * 16:X2}")).ToList();

        await service.ApplyAsync(new LightingConfig().With(mirror.Id, new LightingSettings { Brightness = 50, Zones = zones }));

        Assert.Equal(new RgbColor(0, 0, 0), laptop.MirrorColor(1));
        Assert.Null(laptop.MirrorColor(2));
        Assert.Equal(new RgbColor(0, 0, 0x60), laptop.MirrorColor(7));
        Assert.Equal(Convert.FromHexString("00003200000000000302000000000000"), laptop.LightBarBacklight);
        Assert.Equal(keyboardBefore, laptop.Backlight);
    }

    [Fact]
    public async Task An_effect_is_one_record_for_every_bar()
    {
        var (service, laptop, mirror, _) = Create();

        await service.ApplyAsync(new LightingConfig().With(mirror.Id,
            new LightingSettings { Effect = LightingEffect.Snake, Speed = 4, Brightness = 100, EffectColor = "#00AEC7" }));

        var record = laptop.LightBarBacklight;
        Assert.Equal((byte)LightBarEffect.Snake, record[0]);
        Assert.Equal(4, record[1]);
        Assert.Equal(100, record[2]);
        Assert.Equal(LightBarProtocol.BacklightDevice, record[9]);
    }

    [Fact]
    public async Task The_mirror_refuses_twinkling()
    {
        var (service, _, mirror, _) = Create();
        var notices = new List<ControlNotice>();
        service.Notice += notices.Add;

        await service.ApplyAsync(new LightingConfig().With(mirror.Id, new LightingSettings { Effect = LightingEffect.Twinkling }));

        Assert.Equal(LightingLocation.InfinityMirror, Assert.Single(notices).Light);
    }

    [Fact]
    public async Task Logo_takes_colour_brightness_and_speed_then_its_behaviour()
    {
        var (service, laptop, _, logo) = Create();

        await service.ApplyAsync(new LightingConfig().With(logo.Id,
            new LightingSettings { Effect = LightingEffect.Breathing, Speed = 3, Brightness = 75, EffectColor = "#00AEC7" }));
        Assert.Equal((0x0003_4BC7_AE00_01UL, 0x0003_0001UL), laptop.Logo);

        await service.ApplyAsync(new LightingConfig().With(logo.Id,
            new LightingSettings { Effect = LightingEffect.Neon, Speed = 5, Brightness = 100, EffectColor = "#00AEC7" }));
        Assert.Equal((0x0005_6400_0000_01UL, 0x0004_0001UL), laptop.Logo);
    }

    [Fact]
    public void Detection_finds_the_predators_lights_and_none_on_an_an515_57()
    {
        var predator = new SimulatedMachine(SimulatedModel.Predator2024);
        var caps = CapabilityProbe.Probe(new AcerDevice(predator.OpenFirmware()), predator.ReadSmbios(), predator.Model);

        Assert.Equal([LightingLocation.Keyboard, LightingLocation.InfinityMirror, LightingLocation.Logo], caps.Lights.Select(l => l.Location));
        Assert.Equal([new LightBar(LightBarId.Rear, 7)], caps.LightBars);
        Assert.Equal(16, caps.Keyboard.LedArrayLength);
        Assert.Contains("GetGamingLED(0x10) = status 0", caps.Diagnostics, StringComparison.Ordinal);

        var nitro = new SimulatedMachine(SimulatedModel.Nitro2021);
        var nitroCaps = CapabilityProbe.Probe(new AcerDevice(nitro.OpenFirmware()), nitro.ReadSmbios(), nitro.Model);
        Assert.Equal([LightingLocation.Keyboard], nitroCaps.Lights.Select(l => l.Location));
        Assert.Empty(nitroCaps.LightBars);
        Assert.Contains("GetGamingLED(0x10) = status 2, no layout", nitroCaps.Diagnostics, StringComparison.Ordinal);
    }
}

public sealed class PredatorLightingIpcTests : IAsyncLifetime
{
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
    public async Task Every_light_crosses_the_wire_and_is_applied()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshot = await _service.GetSnapshotAsync(ct);

        Assert.Equal(["Keyboard", "InfinityMirror", "Logo"], snapshot.Capabilities.Lights.Select(l => l.Id));
        Assert.Equal([new LightBar(LightBarId.Rear, 7)], snapshot.Capabilities.LightBars);
        var mirror = snapshot.Capabilities.Lights[1];
        Assert.Equal(5, mirror.Traits(LightingEffect.Lightning)!.MaxSpeed);
        // The keyboard can be read at start; the light bars and the logo can't.
        Assert.Equal(["Keyboard"], snapshot.Lighting!.Keys);

        var config = new LightingConfig()
            .With("InfinityMirror", new LightingSettings { Effect = LightingEffect.Stack, Speed = 2 })
            .With("Logo", new LightingSettings { Effect = LightingEffect.Neon });
        await _service.SetLightingAsync(config, ct);

        var settings = (await _service.GetSnapshotAsync(ct)).Settings.Lighting;
        Assert.Equal(LightingEffect.Stack, settings.For("InfinityMirror")!.Effect);
        Assert.Equal(LightingEffect.Neon, settings.For("Logo")!.Effect);
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

public class KeyboardZoneTests
{
    private static DeviceCapabilities Probe(params AcerSmbiosRecord[] records) =>
        CapabilityProbe.Probe(new AcerDevice(new SimulatedTransport(SimulatedModel.Predator2024)), new AcerSmbios(2, 0x5B, records, []));

    [Fact]
    public void The_zone_count_is_the_one_the_bios_names()
    {
        // The firmware drops a fourth zone unless SMBIOS record 0x08 says 4, so a 3-zone keyboard must not show one.
        Assert.Equal(3, Probe(new(0x08, 3), new(0x0A, 2)).Keyboard.Zones);
        Assert.Equal(4, Probe(new(0x08, 4), new(0x0A, 2)).Keyboard.Zones);
        Assert.Equal(1, Probe(new(0x08, 1), new(0x0A, 2)).Keyboard.Zones);
        Assert.Equal(4, Probe(new AcerSmbiosRecord(0x0A, 2)).Keyboard.Zones);

        // 0 is a per-key keyboard, which the zoned controls don't reach.
        var perKey = Probe(new(0x08, 0), new(0x0A, 2));
        Assert.False(perKey.Keyboard.RgbBacklight);
        Assert.DoesNotContain(perKey.Lights, l => l.Backend == LightingBackendKind.EcKeyboard);
    }
}

public class LightingSettingsTests
{
    private static readonly LightingDeviceInfo Logo = new("Logo", LightingLocation.Logo, LightingBackendKind.EcLogo)
    {
        Zones = 1,
        Effects = [new EffectTraits(LightingEffect.Breathing, Color: true) { MinSpeed = 1, MaxSpeed = 5 }],
        BrightnessLevels = [0, 50, 100],
    };

    [Fact]
    public void Adapting_keeps_an_effect_the_other_light_has_within_its_ranges()
    {
        var keyboard = new LightingSettings { Effect = LightingEffect.Breathing, Speed = 9, Brightness = 75, EffectColor = "#00FF00" };

        var adapted = keyboard.AdaptTo(Logo);

        Assert.Equal(LightingEffect.Breathing, adapted.Effect);
        Assert.Equal(5, adapted.Speed);
        Assert.Contains(adapted.Brightness, Logo.BrightnessLevels);
        Assert.Equal("#00FF00", adapted.EffectColor);
        Assert.Single(adapted.Zones);
    }

    [Fact]
    public void Adapting_an_effect_the_other_light_lacks_gives_its_colour_as_static()
    {
        var keyboard = new LightingSettings { Effect = LightingEffect.Wave, EffectColor = "#123456" };

        var adapted = keyboard.AdaptTo(Logo);

        Assert.Equal(LightingEffect.Static, adapted.Effect);
        Assert.Equal(new ZoneSetting(true, "#123456"), Assert.Single(adapted.Zones));
    }

    [Fact]
    public void Adapting_static_zones_uses_the_first_lit_zone_when_the_counts_differ()
    {
        var keyboard = new LightingSettings { Zones = [new(false, "#FFFFFF"), new(true, "#FF0000"), new(true, "#00FF00"), new(true, "#0000FF")] };

        var adapted = keyboard.AdaptTo(Logo);

        Assert.Equal(new ZoneSetting(true, "#FF0000"), Assert.Single(adapted.Zones));
    }

    [Fact]
    public void Settings_compare_by_zone_contents()
    {
        var a = new LightingSettings();
        var b = a with { Zones = [.. a.Zones] };

        Assert.NotEqual(a, b); // records compare lists by reference
        Assert.True(a.SameAs(b));
        Assert.False(a.SameAs(b with { Zones = [new(false, "#000000")] }));
    }
}

public sealed class KeyboardLightingPersistenceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static OpenSenseEngine StartEngine(SimulatedMachine machine, string settingsPath, FakeTimeProvider? time = null)
    {
        var engine = new OpenSenseEngine(machine, settingsPath, NullLogger<OpenSenseEngine>.Instance, time);
        engine.Start();
        return engine;
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Condition was not met before the timeout.");
            await Task.Delay(20, ct);
        }
    }

    [Fact]
    public async Task First_run_adopts_firmware_keyboard_lighting_without_overwriting_hardware()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            // Seed pre-existing NitroSense/BIOS state before OpenSense's very first start.
            var firmware = new SimulatedMachine(SimulatedModel.Nitro2022);
            using var engine = new OpenSenseEngine(new SeededKeyboardMachine(firmware,
                KeyboardProtocol.BacklightPayload(KeyboardEffect.Wave, 6, 75, KeyboardDirection.Left, default), timeoutBrightness: 75),
                settingsPath, NullLogger<OpenSenseEngine>.Instance);
            engine.Start();
            await engine.LightingStartupTask.WaitAsync(Timeout, ct);

            var snapshot = await engine.GetSnapshotAsync(ct);
            var saved = snapshot.Settings.Lighting.For(EcKeyboardBackend.Id);
            Assert.NotNull(saved);
            Assert.Equal(LightingEffect.Wave, saved.Effect);
            Assert.Equal(75, saved.Brightness);
            Assert.Equal(LightingDirection.Left, saved.Direction);
            Assert.Equal(75, new SettingsStore<MachineSettings>(settingsPath).Load().Lighting.For(EcKeyboardBackend.Id)!.Brightness);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Turning_off_backlight_via_fn_hotkey_persists_across_cold_boot()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            var time = new FakeTimeProvider();
            var machine1 = new SimulatedMachine(SimulatedModel.Nitro2022);
            using (var engine1 = StartEngine(machine1, settingsPath, time))
            {
                await engine1.LightingStartupTask.WaitAsync(Timeout, ct);
                await engine1.SetKeyboardAsync(new KeyboardSettings { BacklightAutoOff = true }, ct);
                await engine1.SetLightingAsync(new LightingConfig().With(EcKeyboardBackend.Id,
                    new LightingSettings { Effect = LightingEffect.Wave, Speed = 4, Brightness = 100, Direction = LightingDirection.Left }), ct);

                var turnedOff = new TaskCompletionSource<MachineSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
                engine1.SettingsChanged += (_, s) =>
                {
                    if (s.Lighting.For(EcKeyboardBackend.Id)?.Brightness == 0)
                        turnedOff.TrySetResult(s);
                };

                // Simulate Fn+F9 updating APGeAction 0x84 brightness even when GetKeyboardBacklight stays stale.
                machine1.Laptop!.OverwriteBacklightTimeoutBrightness(0);
                machine1.Events.Raise(1, 0x84, 0, 0);
                time.Advance(TimeSpan.FromMilliseconds(200));

                var updated = await turnedOff.Task.WaitAsync(Timeout, ct);
                var saved = updated.Lighting.For(EcKeyboardBackend.Id)!;
                Assert.Equal(0, saved.Brightness);
                Assert.Equal(LightingEffect.Wave, saved.Effect);
                Assert.Equal(LightingDirection.Left, saved.Direction);
            }

            // Simulate a full shutdown & cold boot where BIOS resets keyboard lighting back to 100% Static.
            var resumeTime = new FakeTimeProvider();
            var machine2 = new SimulatedMachine(SimulatedModel.Nitro2022);
            using var engine2 = StartEngine(machine2, settingsPath, resumeTime);
            await engine2.LightingStartupTask.WaitAsync(Timeout, ct);
            await WaitForConditionAsync(() => machine2.Laptop!.Backlight[2] == 0 && machine2.Laptop.BacklightTimeoutBrightness == 0, ct);

            Assert.Equal((byte)KeyboardEffect.Wave, machine2.Laptop!.Backlight[0]);
            Assert.Equal(0, machine2.Laptop.Backlight[2]);
            Assert.Equal(0, machine2.Laptop.BacklightTimeoutBrightness);

            // Suspend and wake while Off must also remain Off (even if Acer's agent briefly restores 100%).
            engine2.NotifySuspend();
            engine2.NotifyResume();
            machine2.Laptop.OverwriteBacklight(KeyboardProtocol.BacklightPayload(KeyboardEffect.Neon, 5, 100, KeyboardDirection.Right, default));
            machine2.Laptop.OverwriteBacklightTimeoutBrightness(100);
            resumeTime.Advance(TimeSpan.FromSeconds(6));
            await engine2.ResumeLightingTask.WaitAsync(Timeout, ct);
            Assert.Equal((byte)KeyboardEffect.Wave, machine2.Laptop.Backlight[0]);
            Assert.Equal(0, machine2.Laptop.Backlight[2]);
            Assert.Equal(0, machine2.Laptop.BacklightTimeoutBrightness);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Switching_mode_and_brightness_externally_memorizes_and_syncs_at_next_boot()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            var time = new FakeTimeProvider();
            var machine1 = new SimulatedMachine(SimulatedModel.Nitro2022);
            using (var engine1 = StartEngine(machine1, settingsPath, time))
            {
                await engine1.LightingStartupTask.WaitAsync(Timeout, ct);
                await engine1.SetLightingAsync(new LightingConfig().With(EcKeyboardBackend.Id,
                    new LightingSettings { Effect = LightingEffect.Static, Brightness = 0 }), ct);

                var changed = new TaskCompletionSource<MachineSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
                engine1.SettingsChanged += (_, s) =>
                {
                    if (s.Lighting.For(EcKeyboardBackend.Id) is { Effect: LightingEffect.Breathing, Brightness: 50 })
                        changed.TrySetResult(s);
                };

                // Simulate EC changing both effect and brightness without emitting APGeEvent (caught by fallback poll).
                machine1.Laptop!.OverwriteBacklight(KeyboardProtocol.BacklightPayload(
                    KeyboardEffect.Breathing, 7, 50, KeyboardDirection.Right, new RgbColor(0x00, 0xFF, 0x88)));
                machine1.Laptop.OverwriteBacklightTimeoutBrightness(50);
                time.Advance(TimeSpan.FromSeconds(2));

                var updated = await changed.Task.WaitAsync(Timeout, ct);
                var saved = updated.Lighting.For(EcKeyboardBackend.Id)!;
                Assert.Equal(LightingEffect.Breathing, saved.Effect);
                Assert.Equal(50, saved.Brightness);
                Assert.Equal(7, saved.Speed);
                Assert.Equal("#00FF88", saved.EffectColor);
            }

            // Cold boot into a freshly reset simulated laptop and ensure Breathing 50% is synced back.
            var machine2 = new SimulatedMachine(SimulatedModel.Nitro2022);
            using var engine2 = StartEngine(machine2, settingsPath);
            await engine2.LightingStartupTask.WaitAsync(Timeout, ct);

            Assert.Equal((byte)KeyboardEffect.Breathing, machine2.Laptop!.Backlight[0]);
            Assert.Equal(50, machine2.Laptop.Backlight[2]);
            Assert.Equal(50, machine2.Laptop.BacklightTimeoutBrightness);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareShutdown_and_Suspend_sync_changes_dim_hardware_and_restore_saved_settings_on_wake()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            var time = new FakeTimeProvider();
            var machine = new SimulatedMachine(SimulatedModel.Nitro2022);
            using var engine = StartEngine(machine, settingsPath, time);
            await engine.LightingStartupTask.WaitAsync(Timeout, ct);
            await engine.SetLightingAsync(new LightingConfig().With(EcKeyboardBackend.Id,
                new LightingSettings { Effect = LightingEffect.Wave, Brightness = 75, Direction = LightingDirection.Right }), ct);

            // Suspend turns hardware off while preserving Wave 75% in settings.
            engine.NotifySuspend();
            Assert.Equal(0, machine.Laptop!.Backlight[2]);
            Assert.Equal(0, machine.Laptop.BacklightTimeoutBrightness);
            Assert.Equal(75, (await engine.GetSnapshotAsync(ct)).Settings.Lighting.For(EcKeyboardBackend.Id)!.Brightness);

            // Resume holds external sync while Acer's agent briefly overwrites, then restores Wave 75%.
            engine.NotifyResume();
            machine.Laptop.OverwriteBacklight(KeyboardProtocol.BacklightPayload(KeyboardEffect.Neon, 5, 100, KeyboardDirection.Right, default));
            machine.Laptop.OverwriteBacklightTimeoutBrightness(100);
            time.Advance(TimeSpan.FromSeconds(2));
            await engine.SyncKeyboardLightingAsync(); // Ignored during resume hold
            Assert.Equal(LightingEffect.Wave, (await engine.GetSnapshotAsync(ct)).Settings.Lighting.For(EcKeyboardBackend.Id)!.Effect);

            time.Advance(TimeSpan.FromSeconds(4));
            await engine.ResumeLightingTask.WaitAsync(Timeout, ct);
            Assert.Equal((byte)KeyboardEffect.Wave, machine.Laptop.Backlight[0]);
            Assert.Equal(75, machine.Laptop.Backlight[2]);
            Assert.Equal(75, machine.Laptop.BacklightTimeoutBrightness);

            // Fn+F9 down to 25% right before Windows OS shutdown is saved to disk while hardware turns off for shutdown.
            machine.Laptop.OverwriteBacklightTimeoutBrightness(25);
            engine.PrepareShutdown();

            var disk = new SettingsStore<MachineSettings>(settingsPath).Load().Lighting.For(EcKeyboardBackend.Id)!;
            Assert.Equal(LightingEffect.Wave, disk.Effect);
            Assert.Equal(25, disk.Brightness);
            Assert.Equal(0, machine.Laptop.Backlight[2]);
            Assert.Equal(0, machine.Laptop.BacklightTimeoutBrightness);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task All_static_zones_off_sends_zero_brightness_without_clobbering_ui_brightness()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "settings.json");
        try
        {
            var time = new FakeTimeProvider();
            var machine = new SimulatedMachine(SimulatedModel.Nitro2022);
            using var engine = StartEngine(machine, settingsPath, time);
            await engine.LightingStartupTask.WaitAsync(Timeout, ct);

            var allZonesOff = Enumerable.Repeat(new ZoneSetting(false, "#FF3B30"), 4).ToList();
            await engine.SetLightingAsync(new LightingConfig().With(EcKeyboardBackend.Id,
                new LightingSettings { Effect = LightingEffect.Static, Brightness = 100, Zones = allZonesOff }), ct);

            Assert.Equal(0, machine.Laptop!.Backlight[2]);
            Assert.Equal(0, machine.Laptop.BacklightTimeoutBrightness);

            // Fallback poll matches the post-apply baseline and must not overwrite saved Brightness = 100.
            time.Advance(TimeSpan.FromSeconds(2));
            await engine.SyncKeyboardLightingAsync();
            Assert.Equal(100, (await engine.GetSnapshotAsync(ct)).Settings.Lighting.For(EcKeyboardBackend.Id)!.Brightness);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class SeededKeyboardMachine(SimulatedMachine inner, byte[] initialBacklight, int timeoutBrightness) : IMachine
    {
        public string? Model => inner.Model;
        public string? BiosVersion => inner.BiosVersion;
        public string? SerialNumber => inner.SerialNumber;
        public IWmiTransport OpenFirmware()
        {
            var laptop = (SimulatedTransport)inner.OpenFirmware();
            laptop.OverwriteBacklight(initialBacklight);
            laptop.OverwriteBacklightTimeoutBrightness(timeoutBrightness);
            return laptop;
        }
        public IFirmwareEvents OpenFirmwareEvents() => inner.OpenFirmwareEvents();
        public IPowerSource OpenPowerSource() => inner.OpenPowerSource();
        public ISystemPower OpenSystemPower() => inner.OpenSystemPower();
        public BatteryHealth? ReadBatteryHealth() => inner.ReadBatteryHealth();
        public IHidBus OpenHid() => inner.OpenHid();
        public AcerSmbios ReadSmbios() => inner.ReadSmbios();
        public IReadOnlyList<AcpiTable> ReadAcpiTables() => inner.ReadAcpiTables();
        public ILoadMonitor OpenLoadMonitor() => inner.OpenLoadMonitor();
        public DirectSensors OpenSensors() => inner.OpenSensors();
        public IBootLogoStore? OpenBootLogo(Action<string> log) => inner.OpenBootLogo(log);
        public PixelSize? ReadInternalScreen() => inner.ReadInternalScreen();
    }
}
