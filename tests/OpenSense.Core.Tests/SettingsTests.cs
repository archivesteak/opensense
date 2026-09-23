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
                Mode = FanControlMode.Curve,
                Curves = new Dictionary<FanId, CurveFanSetting>
                {
                    [FanId.Cpu] = new(FanCurve.From((35, 10), (70, 60), (90, 100)), TemperatureSource.Hottest),
                    [FanId.Gpu] = new(CurvePresets.Silent, TemperatureSource.Gpu),
                },
                OperatingMode = OperatingMode.Performance,
                Safety = new SafetySettings { EmergencyTemperatureC = 90, MinimumPercent = 20 },
            },
            Keyboard = new KeyboardSettings
            {
                Lighting = new LightingSettings { Effect = KeyboardEffect.Wave, Speed = 7, Direction = KeyboardDirection.Up, EffectColor = "#00FF88" },
                WindowsKey = false,
            },
            Overrides = new CapabilityOverrides { OperatingModes = true },
            PollIntervalMs = 500,
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(FanControlMode.Curve, loaded.Profile.Mode);
        Assert.Equal(original.Profile.CurveFor(FanId.Cpu), loaded.Profile.CurveFor(FanId.Cpu));
        Assert.Equal(TemperatureSource.Gpu, loaded.Profile.CurveFor(FanId.Gpu).Source);
        Assert.Equal(OperatingMode.Performance, loaded.Profile.OperatingMode);
        Assert.Equal(original.Profile.Safety, loaded.Profile.Safety);
        Assert.Equal(KeyboardEffect.Wave, loaded.Keyboard.Lighting!.Effect);
        Assert.Equal("#00FF88", loaded.Keyboard.Lighting.EffectColor);
        Assert.False(loaded.Keyboard.WindowsKey);
        Assert.Null(loaded.Keyboard.LcdOverdrive);
        Assert.True(loaded.Overrides.OperatingModes);
        Assert.Equal(500, loaded.PollIntervalMs);
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
        Assert.Equal(95, loaded.Profile.Safety.EmergencyTemperatureC);
        Assert.Equal(CurvePresets.Balanced, loaded.Profile.CurveFor(FanId.Cpu).Curve);
        Assert.Equal(1000, loaded.PollIntervalMs);
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
