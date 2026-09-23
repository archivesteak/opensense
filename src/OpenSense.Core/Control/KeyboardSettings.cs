using OpenSense.Core.Hardware;

namespace OpenSense.Core.Control;

public sealed record ZoneSetting(bool On, string Color);

/// <summary>What the keyboard backlight should show.</summary>
public sealed record LightingSettings
{
    public KeyboardEffect Effect { get; init; } = KeyboardEffect.Static;

    /// <summary>0..100, one of <see cref="KeyboardProtocol.BrightnessLevels"/>.</summary>
    public int Brightness { get; init; } = 100;

    public int Speed { get; init; } = 5;

    public KeyboardDirection Direction { get; init; } = KeyboardDirection.Right;

    /// <summary>Colour for breathing / wave / shifting / zoom.</summary>
    public string EffectColor { get; init; } = "#FF3B30";

    /// <summary>Static mode: per-zone on/off and colour.</summary>
    public IReadOnlyList<ZoneSetting> Zones { get; init; } =
        [new(true, "#FF3B30"), new(true, "#FF3B30"), new(true, "#FF3B30"), new(true, "#FF3B30")];

    public ZoneSetting Zone(int index) => index < Zones.Count ? Zones[index] : new ZoneSetting(true, "#FF3B30");
}

/// <summary>
/// Keyboard and display settings OpenSense owns. A null member means "leave the firmware as it is",
/// so a first launch never overwrites what NitroSense configured.
/// </summary>
public sealed record KeyboardSettings
{
    public LightingSettings? Lighting { get; init; }

    public bool? BacklightAutoOff { get; init; }

    public bool? WindowsKey { get; init; }

    public bool? LcdOverdrive { get; init; }
}

/// <summary>What the firmware reports for the keyboard right now.</summary>
public sealed record KeyboardState(
    KeyboardEffect? Effect,
    int? Brightness,
    int? Speed,
    KeyboardDirection? Direction,
    RgbColor? EffectColor,
    bool? BacklightAutoOff,
    bool? WindowsKey,
    bool? LcdOverdrive)
{
    public static KeyboardState Read(AcerDevice device, KeyboardCapabilities caps)
    {
        var record = caps.RgbBacklight ? device.GetKeyboardBacklight() : null;
        var profile = caps.WindowsKey || caps.LcdOverdrive ? device.GetGamingProfile() : null;
        var timeout = caps.BacklightHotkey is { } h ? device.GetBacklightTimeout(h) : null;
        return new KeyboardState(
            record is { Length: >= 8 } && Enum.IsDefined((KeyboardEffect)record[0]) ? (KeyboardEffect)record[0] : null,
            record is { Length: >= 8 } ? record[2] : null,
            record is { Length: >= 8 } && record[1] is >= KeyboardProtocol.MinSpeed and <= KeyboardProtocol.MaxSpeed ? record[1] : null,
            record is { Length: >= 8 } && Enum.IsDefined((KeyboardDirection)record[4]) ? (KeyboardDirection)record[4] : null,
            record is { Length: >= 8 } ? new RgbColor(record[5], record[6], record[7]) : null,
            timeout is { } t ? t.TimeoutSeconds > 0 : null,
            caps.WindowsKey && profile is { } wp ? KeyboardProtocol.WindowsKeyValue(wp) : null,
            caps.LcdOverdrive && profile is { } op ? KeyboardProtocol.LcdOverdriveValue(op) : null);
    }

    /// <summary>Lighting settings matching the firmware's current effect (zone colours are not readable).</summary>
    public LightingSettings ToLighting(KeyboardCapabilities caps)
    {
        var defaults = new LightingSettings();
        var zoneColors = caps.DefaultZoneColors.Count > 0 ? caps.DefaultZoneColors : [RgbColor.FromHex(defaults.EffectColor)];
        return defaults with
        {
            Effect = Effect ?? defaults.Effect,
            Brightness = Brightness is { } b ? KeyboardProtocol.BrightnessLevels.MinBy(l => Math.Abs(l - b)) : defaults.Brightness,
            Speed = Speed ?? defaults.Speed,
            Direction = Direction ?? defaults.Direction,
            EffectColor = EffectColor is { } c && c != default ? c.ToHex() : defaults.EffectColor,
            Zones = [.. Enumerable.Range(0, Math.Max(caps.Zones, 1)).Select(i => new ZoneSetting(true, zoneColors[Math.Min(i, zoneColors.Count - 1)].ToHex()))],
        };
    }
}
