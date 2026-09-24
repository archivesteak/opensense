using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Control;
using OpenSense.Core.Hardware;
using Windows.UI;

namespace OpenSense.App.ViewModels;

public sealed partial class ZoneViewModel(int index) : ObservableObject
{
    public int Index { get; } = index;

    public string Name => Strings.Format("Lighting_Zone", Index + 1);

    [ObservableProperty]
    public partial bool On { get; set; } = true;

    [ObservableProperty]
    public partial Color Color { get; set; } = Color.FromArgb(255, 0xFF, 0x3B, 0x30);

    public ZoneSetting ToSetting() => new(On, ColorHex.From(Color));
}

public sealed record EffectOption(KeyboardEffect Effect, string Name, string Description);

public static class ColorHex
{
    public static string From(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color To(string hex)
    {
        var rgb = RgbColor.FromHex(hex);
        return Color.FromArgb(255, rgb.R, rgb.G, rgb.B);
    }
}

/// <summary>RGB keyboard: static zones or a hardware effect, plus brightness.</summary>
public sealed partial class LightingViewModel : ObservableObject
{
    private static readonly TimeSpan ApplyDelay = TimeSpan.FromMilliseconds(80);

    private readonly DeviceSession _session;
    private readonly DispatcherQueueTimer _applyTimer;
    private bool _loading;

    public LightingViewModel(DispatcherQueue dispatcher, DeviceSession session)
    {
        _session = session;
        _applyTimer = dispatcher.CreateTimer();
        _applyTimer.Interval = ApplyDelay;
        _applyTimer.IsRepeating = false;
        _applyTimer.Tick += (_, _) => Apply();
    }

    public static IReadOnlyList<EffectOption> Effects { get; } =
    [
        new(KeyboardEffect.Breathing, Strings.Get("Effect_Breathing"), Strings.Get("Effect_Breathing_Description")),
        new(KeyboardEffect.Neon, Strings.Get("Effect_Neon"), Strings.Get("Effect_Neon_Description")),
        new(KeyboardEffect.Wave, Strings.Get("Effect_Wave"), Strings.Get("Effect_Wave_Description")),
        new(KeyboardEffect.Shifting, Strings.Get("Effect_Shifting"), Strings.Get("Effect_Shifting_Description")),
        new(KeyboardEffect.Zoom, Strings.Get("Effect_Zoom"), Strings.Get("Effect_Zoom_Description")),
    ];

    /// <summary>In <see cref="KeyboardDirection"/> order.</summary>
    public static IReadOnlyList<string> Directions { get; } =
        [Strings.Get("Direction_Left"), Strings.Get("Direction_Right"), Strings.Get("Direction_Up"), Strings.Get("Direction_Down")];

    /// <summary>In <see cref="KeyboardProtocol.BrightnessLevels"/> order.</summary>
    public static IReadOnlyList<string> BrightnessNames { get; } =
        [Strings.Get("Brightness_Off"), Units.Percent(25), Units.Percent(50), Units.Percent(75), Units.Percent(100)];

    /// <summary>Quick colour picks.</summary>
    public static IReadOnlyList<Color> Swatches { get; } =
    [
        Color.FromArgb(255, 0xFF, 0x3B, 0x30), Color.FromArgb(255, 0xFF, 0x95, 0x00), Color.FromArgb(255, 0xFF, 0xD6, 0x0A),
        Color.FromArgb(255, 0x34, 0xC7, 0x59), Color.FromArgb(255, 0x00, 0xC7, 0xBE), Color.FromArgb(255, 0x0A, 0x84, 0xFF),
        Color.FromArgb(255, 0x5E, 0x5C, 0xE6), Color.FromArgb(255, 0xBF, 0x5A, 0xF2), Color.FromArgb(255, 0xFF, 0x2D, 0x55),
        Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
    ];

    public ObservableCollection<ZoneViewModel> Zones { get; } = [];

    [ObservableProperty]
    public partial bool Available { get; set; }

    /// <summary>0 = static zones, 1 = effect.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatic), nameof(IsEffect))]
    public partial int ModeIndex { get; set; }

    public bool IsStatic => ModeIndex == 0;

    public bool IsEffect => ModeIndex == 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Effect), nameof(EffectDescription), nameof(EffectUsesColor), nameof(EffectUsesDirection))]
    public partial int EffectIndex { get; set; }

    public KeyboardEffect Effect => Effects[Math.Clamp(EffectIndex, 0, Effects.Count - 1)].Effect;

    public string EffectDescription => Effects[Math.Clamp(EffectIndex, 0, Effects.Count - 1)].Description;

    public bool EffectUsesColor => KeyboardProtocol.UsesColor(Effect);

    public bool EffectUsesDirection => KeyboardProtocol.UsesDirection(Effect);

    [ObservableProperty]
    public partial double Speed { get; set; } = 5;

    [ObservableProperty]
    public partial int DirectionIndex { get; set; } = 1;

    [ObservableProperty]
    public partial Color EffectColor { get; set; } = Color.FromArgb(255, 0xFF, 0x3B, 0x30);

    [ObservableProperty]
    public partial int BrightnessIndex { get; set; } = 4;

    /// <summary>The "any colour" picker of "all zones": what it picks goes to every zone.</summary>
    [ObservableProperty]
    public partial Color AllZonesColor { get; set; } = Color.FromArgb(255, 0xFF, 0x3B, 0x30);

    /// <summary>Changes whenever anything visible in the keyboard preview changes.</summary>
    [ObservableProperty]
    public partial int PreviewRevision { get; set; }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        _loading = true;
        _applyTimer.Stop();
        var caps = _session.Capabilities.Keyboard;
        Available = caps.RgbBacklight;
        var lighting = _session.Settings.Keyboard.Lighting
            ?? _session.InitialKeyboard?.ToLighting(caps)
            ?? new LightingSettings();

        ModeIndex = lighting.Effect == KeyboardEffect.Static ? 0 : 1;
        EffectIndex = Math.Max(0, Effects.ToList().FindIndex(e => e.Effect == lighting.Effect));
        Speed = lighting.Speed;
        DirectionIndex = (int)lighting.Direction - 1;
        EffectColor = ColorHex.To(lighting.EffectColor);
        BrightnessIndex = Math.Max(0, KeyboardProtocol.BrightnessLevels.ToList().IndexOf(lighting.Brightness));

        Zones.Clear();
        for (var i = 0; i < caps.Zones; i++)
        {
            var setting = lighting.Zone(i);
            var zone = new ZoneViewModel(i) { On = setting.On, Color = ColorHex.To(setting.Color) };
            zone.PropertyChanged += (_, _) => Changed();
            Zones.Add(zone);
        }
        AllZonesColor = Zones.FirstOrDefault()?.Color ?? AllZonesColor;
        _loading = false;
    }

    partial void OnModeIndexChanged(int value) => Changed();

    partial void OnEffectIndexChanged(int value) => Changed();

    partial void OnSpeedChanged(double value) => Changed();

    partial void OnDirectionIndexChanged(int value) => Changed();

    partial void OnEffectColorChanged(Color value) => Changed();

    partial void OnBrightnessIndexChanged(int value) => Changed();

    partial void OnAllZonesColorChanged(Color value)
    {
        if (!_loading)
            SetAllZones(value);
    }

    [RelayCommand]
    private void SetAllZones(Color color)
    {
        foreach (var zone in Zones)
        {
            zone.Color = color;
            zone.On = true;
        }
    }

    private void Changed()
    {
        PreviewRevision++;
        // Selection controls briefly report -1 while initialising; never send that to the keyboard.
        if (_loading || ModeIndex < 0 || EffectIndex < 0 || DirectionIndex < 0 || BrightnessIndex < 0)
            return;
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private void Apply()
    {
        var lighting = new LightingSettings
        {
            Effect = IsStatic ? KeyboardEffect.Static : Effect,
            Brightness = KeyboardProtocol.BrightnessLevels[Math.Clamp(BrightnessIndex, 0, 4)],
            Speed = (int)Math.Round(Speed),
            Direction = (KeyboardDirection)(Math.Clamp(DirectionIndex, 0, 3) + 1),
            EffectColor = ColorHex.From(EffectColor),
            Zones = [.. Zones.Select(z => z.ToSetting())],
        };
        _ = _session.SetKeyboardAsync(_session.Settings.Keyboard with { Lighting = lighting });
    }
}
