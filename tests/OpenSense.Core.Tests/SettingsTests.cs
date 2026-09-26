using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;
using OpenSense.Core.Monitoring;
using OpenSense.Core.Settings;

namespace OpenSense.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenSense.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Machine_settings_round_trip_through_json()
    {
        var store = new SettingsStore<MachineSettings>(SettingsPath);
        var original = new MachineSettings
        {
            Profile = new ControlProfile
            {
                Mode = FanControlMode.Custom,
                Manual = new Dictionary<FanId, ManualFanSetting> { [FanId.Cpu] = new(40, UseCurve: true), [FanId.Gpu] = new(20) },
                Curves = new Dictionary<FanId, CurveFanSetting>
                {
                    [FanId.Cpu] = new(FanCurve.From((35, 10), (70, 60), (90, 100))),
                    [FanId.Gpu] = new(AntiThrottle.Gpu(ThermalLimits.GpuDefault)),
                },
                AutoBoost = false,
                OperatingMode = OperatingMode.Performance,
                Safety = new SafetySettings { RestoreAutoOnExit = false },
            },
            Keyboard = new KeyboardSettings { WindowsKey = false },
            Lighting = new LightingConfig().With("Keyboard",
                new LightingSettings { Effect = LightingEffect.Wave, Speed = 7, Direction = LightingDirection.Up, EffectColor = "#00FF88" }),
            Overrides = new CapabilityOverrides { OperatingModes = true },
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(FanControlMode.Custom, loaded.Profile.Mode);
        Assert.Equal(new ManualFanSetting(40, UseCurve: true), loaded.Profile.ManualFor(FanId.Cpu));
        Assert.Equal(original.Profile.CurveFor(FanId.Cpu), loaded.Profile.CurveFor(FanId.Cpu));
        Assert.Equal(AntiThrottle.Gpu(ThermalLimits.GpuDefault), loaded.Profile.CurveFor(FanId.Gpu).Curve);
        Assert.False(loaded.Profile.AutoBoost);
        Assert.Equal(OperatingMode.Performance, loaded.Profile.OperatingMode);
        Assert.Equal(original.Profile.Safety, loaded.Profile.Safety);
        Assert.Equal(LightingEffect.Wave, loaded.Lighting.For("Keyboard")!.Effect);
        Assert.Equal(LightingDirection.Up, loaded.Lighting.For("Keyboard")!.Direction);
        Assert.Equal("#00FF88", loaded.Lighting.For("Keyboard")!.EffectColor);
        Assert.Null(loaded.Keyboard.LegacyLighting);
        Assert.Equal(MachineSettings.CurrentVersion, loaded.Version);
        Assert.False(loaded.Keyboard.WindowsKey);
        Assert.Null(loaded.Keyboard.LcdOverdrive);
        Assert.True(loaded.Overrides.OperatingModes);
    }

    [Fact]
    public void User_settings_round_trip_through_json()
    {
        var store = new SettingsStore<UserSettings>(SettingsPath);
        var original = new UserSettings { Ui = new UiSettings { UseFahrenheit = true, Theme = 2, CloseToTray = false } };

        store.Save(original);

        Assert.Equal(original.Ui, store.Load().Ui);
    }

    [Fact]
    public void Missing_members_fall_back_to_defaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "Version": 1, "Profile": { "Mode": "Max" } }""");

        var loaded = new SettingsStore<MachineSettings>(SettingsPath).Load();

        Assert.Equal(FanControlMode.Max, loaded.Profile.Mode);
        Assert.True(loaded.Profile.AutoBoost);
        Assert.Equal(AntiThrottle.Cpu(ThermalLimits.IntelCpuDefault), loaded.Profile.CurveFor(FanId.Cpu).Curve);
    }

    [Fact]
    public void Settings_from_a_two_fan_version_still_load_and_a_second_gpu_fan_round_trips()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """
            { "Version": 1, "Profile": { "Mode": "Custom",
              "Manual": { "Cpu": { "Percent": 40, "UseCurve": false }, "Gpu": { "Percent": 20, "UseCurve": true } } } }
            """);
        var store = new SettingsStore<MachineSettings>(SettingsPath);

        var loaded = store.Load();
        Assert.Equal(new ManualFanSetting(40), loaded.Profile.ManualFor(FanId.Cpu));
        Assert.Equal(new ManualFanSetting(20, UseCurve: true), loaded.Profile.ManualFor(FanId.Gpu));
        Assert.Equal(new ManualFanSetting(), loaded.Profile.ManualFor(FanId.Gpu2));
        Assert.Equal(AntiThrottle.Gpu(ThermalLimits.GpuDefault), loaded.Profile.CurveFor(FanId.Gpu2).Curve);

        store.Save(loaded with
        {
            Profile = loaded.Profile with
            {
                Manual = new Dictionary<FanId, ManualFanSetting>(loaded.Profile.Manual) { [FanId.Gpu2] = new(70) },
            },
        });
        Assert.Equal(new ManualFanSetting(70), store.Load().Profile.ManualFor(FanId.Gpu2));
        Assert.Contains("\"Gpu2\"", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Version_1_keyboard_lighting_moves_to_the_keyboard_light()
    {
        Directory.CreateDirectory(_directory);
        // As OpenSense 0.2 wrote it.
        File.WriteAllText(SettingsPath, """
            { "Version": 1,
              "Keyboard": { "Lighting": { "Effect": "Shifting", "Brightness": 75, "Speed": 3, "Direction": "Left", "EffectColor": "#FF8000",
                "Zones": [ { "On": true, "Color": "#102030" }, { "On": false, "Color": "#FFFFFF" } ] }, "WindowsKey": false } }
            """);
        var store = new SettingsStore<MachineSettings>(SettingsPath);

        var upgraded = store.Load().Upgrade();

        Assert.Equal(MachineSettings.CurrentVersion, upgraded.Version);
        Assert.Null(upgraded.Keyboard.LegacyLighting);
        Assert.False(upgraded.Keyboard.WindowsKey);
        var keyboard = upgraded.Lighting.For(EcKeyboardBackend.Id);
        Assert.NotNull(keyboard);
        Assert.Equal(LightingEffect.Shifting, keyboard.Effect);
        Assert.Equal(75, keyboard.Brightness);
        Assert.Equal(LightingDirection.Left, keyboard.Direction);
        Assert.Equal(new ZoneSetting(false, "#FFFFFF"), keyboard.Zone(1));

        store.Save(upgraded);
        using var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.False(saved.RootElement.GetProperty("Keyboard").TryGetProperty("Lighting", out _));
        Assert.Equal("Shifting", saved.RootElement.GetProperty("Lighting").GetProperty("Devices").GetProperty("Keyboard").GetProperty("Effect").GetString());
        Assert.Equal(LightingEffect.Shifting, store.Load().Upgrade().Lighting.For(EcKeyboardBackend.Id)!.Effect);
    }

    [Fact]
    public void Corrupt_file_is_kept_aside_and_defaults_are_used()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ not json");

        var loaded = new SettingsStore<MachineSettings>(SettingsPath).Load();

        Assert.Equal(FanControlMode.Auto, loaded.Profile.Mode);
        Assert.True(File.Exists(SettingsPath + ".bad"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
