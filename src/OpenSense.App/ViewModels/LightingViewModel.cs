using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.App.Services;
using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;
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

    /// <summary>The colour the zone's button shows: <see cref="Color"/>, once the preview shows it (<see cref="LightingViewModel.ShowZones"/>).</summary>
    [ObservableProperty]
    public partial Color ShownColor { get; set; } = Color.FromArgb(255, 0xFF, 0x3B, 0x30);

    /// <summary>The zone can be switched off on its own (the keyboard's can; a light with one colour can't).</summary>
    public bool Switchable { get; init; } = true;

    public ZoneSetting ToSetting() => new(On, ColorHex.From(Color));
}

public sealed record EffectOption(EffectTraits Traits, string Name, string Description)
{
    public LightingEffect Effect => Traits.Effect;
}

/// <summary>A light the page can switch to.</summary>
/// <param name="All">Every light, to number two of the same kind ("Light bar 2").</param>
public sealed record LightingDeviceViewModel(LightingDeviceInfo Info, IReadOnlyList<LightingDeviceInfo> All)
{
    public string Name
    {
        get
        {
            var same = All.Where(l => l.Location == Info.Location).ToList();
            return same.Count > 1
                ? Strings.Format("Lighting_NumberedLight", Names.Light(Info.Location), same.IndexOf(Info) + 1)
                : Names.Light(Info.Location);
        }
    }
}

public static class ColorHex
{
    public static string From(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color To(string hex)
    {
        var rgb = RgbColor.FromHex(hex);
        return Color.FromArgb(255, rgb.R, rgb.G, rgb.B);
    }
}

/// <summary>The laptop's lights, one at a time: static zone colours, an effect or a colour per key, plus brightness.</summary>
public sealed partial class LightingViewModel : ObservableObject
{
    private static readonly TimeSpan ApplyDelay = TimeSpan.FromMilliseconds(80);

    /// <summary>The keys each selection button picks (those the keyboard has).</summary>
    private static readonly Dictionary<string, string[]> Presets = new()
    {
        ["wasd"] = ["KeyW", "KeyA", "KeyS", "KeyD"],
        ["arrows"] = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"],
        ["functions"] = [.. Enumerable.Range(1, 12).Select(i => $"F{i}")],
        ["numbers"] = [.. Enumerable.Range(0, 10).Select(i => $"Digit{i}")],
    };

    private readonly DeviceSession _session;
    private readonly DispatcherQueueTimer _applyTimer;
    private readonly Dictionary<string, Color> _keyColors = [];
    private readonly HashSet<string> _selectedKeys = [];
    private bool _loading;

    public LightingViewModel(DispatcherQueue dispatcher, DeviceSession session)
    {
        _session = session;
        _applyTimer = dispatcher.CreateTimer();
        _applyTimer.Interval = ApplyDelay;
        _applyTimer.IsRepeating = false;
        _applyTimer.Tick += (_, _) => Apply();
    }

    /// <summary>Quick colour picks.</summary>
    public static IReadOnlyList<Color> Swatches { get; } =
    [
        Color.FromArgb(255, 0xFF, 0x3B, 0x30), Color.FromArgb(255, 0xFF, 0x95, 0x00), Color.FromArgb(255, 0xFF, 0xD6, 0x0A),
        Color.FromArgb(255, 0x34, 0xC7, 0x59), Color.FromArgb(255, 0x00, 0xC7, 0xBE), Color.FromArgb(255, 0x0A, 0x84, 0xFF),
        Color.FromArgb(255, 0x5E, 0x5C, 0xE6), Color.FromArgb(255, 0xBF, 0x5A, 0xF2), Color.FromArgb(255, 0xFF, 0x2D, 0x55),
        Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
    ];

    public ObservableCollection<LightingDeviceViewModel> Devices { get; } = [];

    public ObservableCollection<ZoneViewModel> Zones { get; } = [];

    [ObservableProperty]
    public partial bool Available { get; set; }

    /// <summary>More than one light, so the page shows a way to switch between them.</summary>
    [ObservableProperty]
    public partial bool HasSeveralDevices { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Device), nameof(HasKeys), nameof(IsKeyboard), nameof(IsLightBar), nameof(IsLogo), nameof(LayoutChoice))]
    public partial int DeviceIndex { get; set; }

    public LightingDeviceInfo? Device => DeviceIndex >= 0 && DeviceIndex < Devices.Count ? Devices[DeviceIndex].Info : null;

    /// <summary>A light per key (a per-key keyboard, the MagForce keys): its preview is the per-key editor.</summary>
    public bool HasKeys => Device is { Keys.Count: > 0 };

    /// <summary>A keyboard lit by zones.</summary>
    public bool IsKeyboard => Device?.Location == LightingLocation.Keyboard && !HasKeys;

    public bool IsLightBar => Device?.Location is LightingLocation.LightBar or LightingLocation.InfinityMirror or LightingLocation.FrontLightBar
        or LightingLocation.LeftLightBar or LightingLocation.RightLightBar or LightingLocation.RearLightBar or LightingLocation.InfiniteRing;

    public bool IsLogo => Device?.Location is LightingLocation.Logo or LightingLocation.CoverLogo or LightingLocation.BaseLogo
        or LightingLocation.TurboKey or LightingLocation.ModeKey;

    /// <summary>0 = static colours, 1 = effect, 2 = a colour per key.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatic), nameof(IsEffect), nameof(IsPerKey))]
    public partial int ModeIndex { get; set; }

    public bool IsStatic => ModeIndex == 0;

    public bool IsEffect => ModeIndex == 1;

    public bool IsPerKey => ModeIndex == 2;

    /// <summary>The light has effects besides static colours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModes))]
    public partial bool HasEffects { get; set; }

    /// <summary>The light takes a colour per key.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModes))]
    public partial bool HasPerKey { get; set; }

    /// <summary>There is more than static colours to choose from.</summary>
    public bool HasModes => HasEffects || HasPerKey;

    /// <summary>The light has zones of its own colour (else one colour for all of it).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleColor))]
    public partial bool HasZones { get; set; }

    public bool IsSingleColor => !HasZones;

    /// <summary>The effects this light offers.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<EffectOption> Effects { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Effect), nameof(EffectDescription), nameof(EffectUsesColor), nameof(EffectUsesDirection),
        nameof(EffectUsesSpeed), nameof(SpeedMinimum), nameof(SpeedMaximum), nameof(EffectTakesRandomColor), nameof(EffectColorChosen))]
    public partial int EffectIndex { get; set; }

    private EffectOption? SelectedEffect =>
        EffectIndex >= 0 && EffectIndex < Effects.Count ? Effects[EffectIndex]
        : Effects.Count > 0 ? Effects[0]
        : null;

    public LightingEffect Effect => SelectedEffect?.Effect ?? LightingEffect.Breathing;

    public string EffectDescription => SelectedEffect?.Description ?? "";

    public bool EffectUsesColor => SelectedEffect?.Traits.Color == true;

    /// <summary>The effect can pick random colours instead of the one chosen.</summary>
    public bool EffectTakesRandomColor => SelectedEffect?.Traits.RandomColor == true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectColorChosen))]
    public partial bool RandomColor { get; set; }

    /// <summary>The effect shows the colour chosen (not random ones).</summary>
    public bool EffectColorChosen => !(RandomColor && EffectTakesRandomColor);

    public bool EffectUsesDirection => SelectedEffect?.Traits.Direction == true;

    public bool EffectUsesSpeed => SelectedEffect?.Traits.Speed == true;

    public double SpeedMinimum => SelectedEffect?.Traits.MinSpeed ?? 1;

    public double SpeedMaximum => SelectedEffect?.Traits.MaxSpeed ?? 9;

    [ObservableProperty]
    public partial double Speed { get; set; } = 5;

    /// <summary>The directions this light's effects move in.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LightingDirection> DirectionValues { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> Directions { get; set; } = [];

    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial Color EffectColor { get; set; } = Color.FromArgb(255, 0xFF, 0x3B, 0x30);

    /// <summary>This light's brightness steps, in its <see cref="LightingDeviceInfo.BrightnessLevels"/> order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> BrightnessNames { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrightnessFraction))]
    public partial int BrightnessIndex { get; set; }

    /// <summary>0..1, for the previews.</summary>
    public double BrightnessFraction =>
        Device is { BrightnessLevels: { Count: > 0 } levels } ? levels[Math.Clamp(BrightnessIndex, 0, levels.Count - 1)] / 100.0 : 1;

    /// <summary>The "any colour" picker of "all zones": what it picks goes to every zone.</summary>
    [ObservableProperty]
    public partial Color AllZonesColor { get; set; } = Color.FromArgb(255, 0xFF, 0x3B, 0x30);

    /// <summary>The layouts the per-key editor can draw.</summary>
    public static IReadOnlyList<KeyboardLayout> LayoutValues { get; } = [KeyboardLayout.Ansi, KeyboardLayout.Iso, KeyboardLayout.Jis];

    public IReadOnlyList<string> LayoutNames { get; } = [.. LayoutValues.Select(Names.Layout)];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Layout))]
    public partial int LayoutIndex { get; set; }

    public KeyboardLayout Layout => LayoutValues[Math.Clamp(LayoutIndex, 0, LayoutValues.Count - 1)];

    /// <summary>The per-key editor lets the layout be chosen (a keyboard with the MagForce keys alone has none to choose).</summary>
    public bool LayoutChoice => Device is { Keys.Count: > 4 };

    /// <summary>The "any colour" picker of the per-key editor: what it picks goes to the selected keys.</summary>
    [ObservableProperty]
    public partial Color KeysColor { get; set; } = Color.FromArgb(255, 0xFF, 0x3B, 0x30);

    /// <summary>Keys are selected (else the colour buttons colour every key).</summary>
    [ObservableProperty]
    public partial bool HasSelection { get; set; }

    /// <summary>A key's colour in the per-key editor; null when it is dark.</summary>
    public Color? KeyColor(string key) => _keyColors.TryGetValue(key, out var color) ? color : null;

    public bool IsKeySelected(string key) => _selectedKeys.Contains(key);

    /// <summary>Selects <paramref name="key"/> alone, or with <paramref name="toggle"/> adds or removes it.</summary>
    public void SelectKey(string key, bool toggle)
    {
        if (!toggle)
            _selectedKeys.Clear();
        if (!toggle || !_selectedKeys.Remove(key))
            _selectedKeys.Add(key);
        SelectionChanged();
    }

    /// <summary>Adds a key dragged over to the selection.</summary>
    public void AddKeyToSelection(string key)
    {
        if (_selectedKeys.Add(key))
            SelectionChanged();
    }

    /// <summary>Selects a group of keys: "wasd", "arrows", "functions", "numbers", or "all".</summary>
    [RelayCommand]
    private void SelectKeys(string group)
    {
        if (Device is not { } device)
            return;
        _selectedKeys.Clear();
        foreach (var key in group == "all" ? device.Keys : Presets.GetValueOrDefault(group, []).Where(device.Keys.Contains))
            _selectedKeys.Add(key);
        SelectionChanged();
    }

    [RelayCommand]
    private void ClearKeySelection()
    {
        _selectedKeys.Clear();
        SelectionChanged();
    }

    /// <summary>Gives the selected keys (every key, when none is selected) <paramref name="color"/>.</summary>
    [RelayCommand]
    private void PaintKeys(Color color)
    {
        foreach (var key in KeysToChange())
            _keyColors[key] = color;
        Changed();
    }

    /// <summary>Darkens the selected keys (every key, when none is selected).</summary>
    [RelayCommand]
    private void TurnOffKeys()
    {
        foreach (var key in KeysToChange())
            _keyColors.Remove(key);
        Changed();
    }

    private IEnumerable<string> KeysToChange() => _selectedKeys.Count > 0 ? [.. _selectedKeys] : Device?.Keys ?? [];

    private void SelectionChanged()
    {
        HasSelection = _selectedKeys.Count > 0;
        PreviewRevision++;
    }

    /// <summary>Changes whenever anything visible in the preview changes.</summary>
    [ObservableProperty]
    public partial int PreviewRevision { get; set; }

    /// <summary>What the light is being set to, as it goes to the service (for the previews); null without a light.</summary>
    public LightingSettings? Current => Build();

    /// <summary>
    /// The zones' buttons take up their colours: the keyboard's preview calls this when it shows a change, which it does
    /// as late as the keyboard does, so the buttons don't change before the preview.
    /// </summary>
    public void ShowZones()
    {
        foreach (var zone in Zones)
            zone.ShownColor = zone.Color;
    }

    /// <summary>Called on the UI thread once the device session is ready, and whenever it changes.</summary>
    public void Attach()
    {
        var shown = Device?.Id;
        _loading = true;
        _applyTimer.Stop();
        Devices.Clear();
        foreach (var light in _session.Capabilities.Lights)
            Devices.Add(new LightingDeviceViewModel(light, _session.Capabilities.Lights));
        Available = Devices.Count > 0;
        HasSeveralDevices = Devices.Count > 1;
        var index = Devices.ToList().FindIndex(d => d.Info.Id == shown);
        DeviceIndex = index >= 0 ? index : Devices.Count > 0 ? 0 : -1;
        Load();
        _loading = false;
    }

    partial void OnDeviceIndexChanging(int value)
    {
        // Send what the light shown so far was set to, before showing the next one.
        if (!_loading && _applyTimer.IsRunning)
        {
            _applyTimer.Stop();
            Apply();
        }
    }

    partial void OnDeviceIndexChanged(int value)
    {
        if (_loading || value < 0)
            return;
        _loading = true;
        Load();
        _loading = false;
    }

    /// <summary>Shows the selected light's settings: the user's, else what it showed at start, else plain colours.</summary>
    private void Load()
    {
        if (Device is not { } device)
        {
            Effects = [];
            Zones.Clear();
            return;
        }
        var lighting = _session.Settings.Lighting.For(device.Id)
            ?? _session.InitialLighting?.GetValueOrDefault(device.Id)
            ?? LightingSettings.For(device);

        var effects = device.Effects.Where(e => e.Effect != LightingEffect.PerKey).ToList();
        HasEffects = effects.Count > 0;
        HasPerKey = device.Offers(LightingEffect.PerKey);
        HasZones = device.Zones > 1 || device.ZoneSwitches;
        BrightnessNames = [.. device.BrightnessLevels.Select(l => l == 0 ? Strings.Get("Brightness_Off") : Units.Percent(l))];
        BrightnessIndex = Math.Max(0, device.BrightnessLevels.ToList().IndexOf(NearestLevel(device, lighting.Brightness)));

        DirectionValues = [.. effects.SelectMany(e => e.Directions).Distinct().Order()];
        Directions = [.. DirectionValues.Select(Names.Direction)];
        Effects = [.. effects.Select(t => new EffectOption(t, Names.Effect(t.Effect), Names.EffectDescription(t, device.Backend)))];
        ModeIndex = lighting.Effect switch
        {
            LightingEffect.PerKey when HasPerKey => 2,
            LightingEffect.Static => 0,
            _ => HasEffects ? 1 : 0,
        };
        EffectIndex = Math.Max(0, Effects.ToList().FindIndex(e => e.Effect == lighting.Effect));
        Speed = Math.Clamp(lighting.Speed, SpeedMinimum, SpeedMaximum);
        DirectionIndex = Math.Max(0, DirectionValues.ToList().IndexOf(lighting.Direction));
        EffectColor = ColorHex.To(lighting.EffectColor);
        RandomColor = lighting.RandomColor;

        _keyColors.Clear();
        foreach (var (key, color) in lighting.KeyColors)
            _keyColors[key] = ColorHex.To(color);
        _selectedKeys.Clear();
        HasSelection = false;
        LayoutIndex = Math.Max(0, LayoutValues.ToList().IndexOf(lighting.Layout ?? device.Layout ?? KeyboardLayout.Ansi));

        Zones.Clear();
        for (var i = 0; i < Math.Max(device.Zones, 1); i++)
        {
            var setting = lighting.Zone(i);
            var color = ColorHex.To(setting.Color);
            var zone = new ZoneViewModel(i) { On = setting.On || !device.ZoneSwitches, Color = color, ShownColor = color, Switchable = device.ZoneSwitches };
            zone.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ZoneViewModel.ShownColor))
                    Changed();
            };
            Zones.Add(zone);
        }
        AllZonesColor = Zones.FirstOrDefault()?.Color ?? AllZonesColor;
        PreviewRevision++;
    }

    private static int NearestLevel(LightingDeviceInfo device, int brightness) =>
        device.BrightnessLevels.Count > 0 ? device.BrightnessLevels.MinBy(l => Math.Abs(l - brightness)) : brightness;

    partial void OnModeIndexChanged(int value)
    {
        // The first time per key: every key in the static colour, rather than all dark.
        if (!_loading && value == 2 && _keyColors.Count == 0 && Device is { } device)
        {
            var color = Zones.FirstOrDefault(z => z.On)?.Color ?? EffectColor;
            foreach (var key in device.Keys)
                _keyColors[key] = color;
        }
        Changed();
    }

    partial void OnEffectIndexChanged(int value) => Changed();

    partial void OnRandomColorChanged(bool value) => Changed();

    partial void OnLayoutIndexChanged(int value) => Changed();

    partial void OnKeysColorChanged(Color value)
    {
        if (!_loading)
            PaintKeys(value);
    }

    partial void OnSpeedChanged(double value) => Changed();

    partial void OnDirectionIndexChanged(int value) => Changed();

    partial void OnEffectColorChanged(Color value) => Changed();

    partial void OnBrightnessIndexChanged(int value) => Changed();

    partial void OnAllZonesColorChanged(Color value)
    {
        if (!_loading)
            SetAllZones(value);
    }

    /// <summary>Colours the zones that are on; zones turned off stay off.</summary>
    [RelayCommand]
    private void SetAllZones(Color color)
    {
        foreach (var zone in Zones.Where(z => z.On))
            zone.Color = color;
    }

    /// <summary>Gives every other light the nearest look it can show to this one's.</summary>
    [RelayCommand]
    private void ApplyToAll()
    {
        if (Device is not { } device || Build() is not { } current)
            return;
        _applyTimer.Stop();
        var config = _session.Settings.Lighting.With(device.Id, current);
        foreach (var other in Devices.Select(d => d.Info).Where(i => i.Id != device.Id))
            config = config.With(other.Id, current.AdaptTo(other));
        _ = _session.SetLightingAsync(config);
    }

    private void Changed()
    {
        PreviewRevision++;
        // Selection controls briefly report -1 while initialising; never send that to a light.
        if (_loading || ModeIndex < 0 || EffectIndex < 0 || DirectionIndex < 0 || BrightnessIndex < 0)
            return;
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private LightingSettings? Build()
    {
        if (Device is not { } device)
            return null;
        return new LightingSettings
        {
            Effect = IsPerKey && HasPerKey ? LightingEffect.PerKey
                : IsStatic || !HasEffects ? LightingEffect.Static
                : Effect,
            Brightness = device.BrightnessLevels.Count > 0 ? device.BrightnessLevels[Math.Clamp(BrightnessIndex, 0, device.BrightnessLevels.Count - 1)] : 100,
            Speed = (int)Math.Round(Speed),
            Direction = DirectionValues.Count > 0 ? DirectionValues[Math.Clamp(DirectionIndex, 0, DirectionValues.Count - 1)] : LightingDirection.Right,
            EffectColor = ColorHex.From(EffectColor),
            RandomColor = RandomColor,
            Zones = [.. Zones.Select(z => z.ToSetting())],
            KeyColors = _keyColors.ToDictionary(k => k.Key, k => ColorHex.From(k.Value)),
            // Kept only when it differs from the layout Acer's software gives the keyboard.
            Layout = HasKeys && Layout != (device.Layout ?? KeyboardLayout.Ansi) ? Layout : null,
        };
    }

    private void Apply()
    {
        if (Device is { } device && Build() is { } lighting)
            _ = _session.SetLightingAsync(_session.Settings.Lighting.With(device.Id, lighting));
    }
}
