namespace OpenSense.Core.Lighting;

/// <summary>
/// A lighting effect, whatever device shows it; each backend turns it into its own encoding. Stored by name (the first
/// eight keep the names settings version 1 used for the keyboard): append only.
/// </summary>
public enum LightingEffect
{
    Static,
    Breathing,
    Neon,
    Wave,
    Shifting,
    Zoom,
    Meteor,
    Twinkling,

    // The Infinity Mirror's.
    Snake,
    Lightning,
    Stack,
    MotionPoint,
    ZoomIn,

    // USB keyboards', the lighting HID interface's and Darfon lights'.
    Ripple,
    Raindrop,
    Fireball,
    Snow,
    Heartbeat,
    Dazzling,
    Matrix,
    Swiping,
    RowWave,
    Racing,
    Sprouting,
    Disco,
    PingPong,
    LightShow,

    /// <summary>The firmware colours the light by the operating mode.</summary>
    FollowOperatingMode,

    /// <summary>A colour per key (<see cref="LightingSettings.KeyColors"/>).</summary>
    PerKey,

    Combo,

    // The MagForce keys'.
    Rainbow,
    Slash,
    Star,
    Blasting,
}

/// <summary>Which way an effect moves. Stored by name: append only.</summary>
public enum LightingDirection
{
    Left,
    Right,
    Up,
    Down,
}

public sealed record ZoneSetting(bool On, string Color);

/// <summary>What one light (the keyboard, a light bar, a logo) should show.</summary>
public sealed record LightingSettings
{
    public const string DefaultColor = "#FF3B30";

    public LightingEffect Effect { get; init; } = LightingEffect.Static;

    /// <summary>0..100, one of the device's <see cref="LightingDeviceInfo.BrightnessLevels"/>.</summary>
    public int Brightness { get; init; } = 100;

    public int Speed { get; init; } = 5;

    public LightingDirection Direction { get; init; } = LightingDirection.Right;

    /// <summary>The colour of an effect that takes one.</summary>
    public string EffectColor { get; init; } = DefaultColor;

    /// <summary>The effect picks random colours instead of <see cref="EffectColor"/> (where it can).</summary>
    public bool RandomColor { get; init; }

    /// <summary>Static colours: per zone on/off and colour.</summary>
    public IReadOnlyList<ZoneSetting> Zones { get; init; } =
        [new(true, DefaultColor), new(true, DefaultColor), new(true, DefaultColor), new(true, DefaultColor)];

    /// <summary>
    /// <see cref="LightingEffect.PerKey"/>'s colours by key (<see cref="LightingDeviceInfo.Keys"/>); a key left out is dark.
    /// </summary>
    public IReadOnlyDictionary<string, string> KeyColors { get; init; } = new Dictionary<string, string>();

    /// <summary>The layout the per-key editor draws; null for the one Acer's software gives the keyboard.</summary>
    public KeyboardLayout? Layout { get; init; }

    public ZoneSetting Zone(int index) => index < Zones.Count ? Zones[index] : new ZoneSetting(true, DefaultColor);

    /// <summary>A key's colour in <see cref="LightingEffect.PerKey"/>; null when it is dark.</summary>
    public string? KeyColor(string key) => KeyColors.GetValueOrDefault(key);

    /// <summary>Records holding lists compare them by reference; this compares their contents.</summary>
    public bool SameAs(LightingSettings? other) =>
        other is not null && this with { Zones = other.Zones, KeyColors = other.KeyColors } == other
        && Zones.SequenceEqual(other.Zones)
        && KeyColors.Count == other.KeyColors.Count
        && KeyColors.All(k => other.KeyColors.TryGetValue(k.Key, out var color) && color == k.Value);

    /// <summary>
    /// The nearest look <paramref name="device"/> can show ("apply to all lights"): the same effect where it has it, else
    /// static colours; the colour, brightness, speed and direction within what it takes; zone for zone when the zone
    /// counts match, else every zone in the first lit zone's colour; key colours for the keys it has.
    /// </summary>
    public LightingSettings AdaptTo(LightingDeviceInfo device)
    {
        var effect = device.Offers(Effect) ? Effect : LightingEffect.Static;
        var traits = device.Traits(effect);
        var color = Effect == LightingEffect.Static
            ? Zones.FirstOrDefault(z => z.On)?.Color ?? EffectColor
            : EffectColor;
        var zones = Effect == LightingEffect.Static && Zones.Count == device.Zones
            ? Zones
            : [.. Enumerable.Repeat(new ZoneSetting(true, color), device.Zones)];
        return this with
        {
            Effect = effect,
            Brightness = device.BrightnessLevels.Count > 0 ? device.BrightnessLevels.MinBy(l => Math.Abs(l - Brightness)) : Brightness,
            Speed = traits is { } t ? Math.Clamp(Speed, t.MinSpeed, t.MaxSpeed) : Speed,
            Direction = traits is { Directions: [var first, ..] } d && !d.Directions.Contains(Direction) ? first : Direction,
            EffectColor = color,
            RandomColor = RandomColor && traits is { RandomColor: true },
            Zones = zones,
            KeyColors = KeyColors.Where(k => device.Keys.Contains(k.Key)).ToDictionary(),
            Layout = device.Keys.Count > 0 ? Layout : null,
        };
    }

    /// <summary>What a light shows before anyone set it or read it: static colours, every zone lit.</summary>
    public static LightingSettings For(LightingDeviceInfo device) => new()
    {
        Brightness = device.BrightnessLevels.Count > 0 ? device.BrightnessLevels[^1] : 100,
        Zones = [.. Enumerable.Repeat(new ZoneSetting(true, DefaultColor), Math.Max(device.Zones, 1))],
    };
}

/// <summary>
/// The lights OpenSense owns, by <see cref="LightingDeviceInfo.Id"/>. A light missing here is left as the firmware has
/// it, so a first launch never overwrites what Acer's software configured.
/// </summary>
public sealed record LightingConfig
{
    public IReadOnlyDictionary<string, LightingSettings> Devices { get; init; } = new Dictionary<string, LightingSettings>();

    public LightingSettings? For(string device) => Devices.GetValueOrDefault(device);

    public LightingConfig With(string device, LightingSettings settings) =>
        this with { Devices = new Dictionary<string, LightingSettings>(Devices) { [device] = settings } };
}
