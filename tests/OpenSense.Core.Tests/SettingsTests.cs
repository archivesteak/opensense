using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
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
                    [FanId.Gpu] = new(CurvePresets.Default),
                },
                AutoBoost = false,
                OperatingMode = OperatingMode.Performance,
                Safety = new SafetySettings { RestoreAutoOnExit = false },
            },
            Keyboard = new KeyboardSettings
            {
                Lighting = new LightingSettings { Effect = KeyboardEffect.Wave, Speed = 7, Direction = KeyboardDirection.Up, EffectColor = "#00FF88" },
                WindowsKey = false,
            },
            Overrides = new CapabilityOverrides { OperatingModes = true },
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(FanControlMode.Custom, loaded.Profile.Mode);
        Assert.Equal(new ManualFanSetting(40, UseCurve: true), loaded.Profile.ManualFor(FanId.Cpu));
        Assert.Equal(original.Profile.CurveFor(FanId.Cpu), loaded.Profile.CurveFor(FanId.Cpu));
        Assert.Equal(CurvePresets.Default, loaded.Profile.CurveFor(FanId.Gpu).Curve);
        Assert.False(loaded.Profile.AutoBoost);
        Assert.Equal(OperatingMode.Performance, loaded.Profile.OperatingMode);
        Assert.Equal(original.Profile.Safety, loaded.Profile.Safety);
        Assert.Equal(KeyboardEffect.Wave, loaded.Keyboard.Lighting!.Effect);
        Assert.Equal("#00FF88", loaded.Keyboard.Lighting.EffectColor);
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
        Assert.Equal(CurvePresets.Default, loaded.Profile.CurveFor(FanId.Cpu).Curve);
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
