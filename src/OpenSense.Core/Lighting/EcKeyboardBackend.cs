using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

/// <summary>The zoned RGB keyboard on the embedded controller.</summary>
public sealed class EcKeyboardBackend(IDeviceDispatcher dispatcher, KeyboardCapabilities caps)
    : EcLightingBackend(dispatcher, Describe(caps))
{
    public const string Id = nameof(LightingLocation.Keyboard);

    /// <summary>The light as Acer's software offers it on this keyboard.</summary>
    public static LightingDeviceInfo Describe(KeyboardCapabilities caps) => new(Id, LightingLocation.Keyboard, LightingBackendKind.EcKeyboard)
    {
        Zones = Math.Max(caps.Zones, 1),
        ZoneSwitches = true,
        Effects = [.. caps.Effects.Select(Traits)],
        BrightnessLevels = KeyboardProtocol.BrightnessLevels,
    };

    private static EffectTraits Traits(KeyboardEffect effect) => new(ToLighting(effect), KeyboardProtocol.UsesColor(effect))
    {
        MinSpeed = KeyboardProtocol.MinSpeed,
        MaxSpeed = KeyboardProtocol.MaxSpeed,
        Directions = KeyboardProtocol.UsesDirection(effect) ? [LightingDirection.Left, LightingDirection.Right] : [],
    };

    /// <summary>
    /// The <c>SetGamingKBBacklight</c> payload <see cref="Apply"/> sends for <paramref name="lighting"/>, with the colour as
    /// chosen (before the model's correction); null for an effect a zoned keyboard doesn't have.
    /// </summary>
    public static byte[]? BacklightPayload(LightingSettings lighting) =>
        Backlight(lighting) is { } b ? KeyboardProtocol.BacklightPayload(b.Effect, b.Speed, lighting.Brightness, b.Direction, b.Color) : null;

    private static (KeyboardEffect Effect, int Speed, KeyboardDirection Direction, RgbColor Color)? Backlight(LightingSettings lighting) =>
        ToKeyboard(lighting.Effect) switch
        {
            null => null,
            // Static's zone colours go separately.
            KeyboardEffect.Static => (KeyboardEffect.Static, 0, KeyboardDirection.Right, default(RgbColor)),
            { } effect => (effect, Math.Clamp(lighting.Speed, KeyboardProtocol.MinSpeed, KeyboardProtocol.MaxSpeed),
                ToKeyboard(lighting.Direction), RgbColor.FromHex(lighting.EffectColor)),
        };

    protected override bool Apply(AcerDevice device, LightingSettings lighting)
    {
        if (!Device.Offers(lighting.Effect) || Backlight(lighting) is not { } backlight)
            return false;
        if (backlight.Effect != KeyboardEffect.Static)
        {
            return device.SetKeyboardBacklight(backlight.Effect, backlight.Speed, lighting.Brightness, backlight.Direction,
                Adjust(backlight.Color));
        }

        var zones = Enumerable.Range(0, caps.Zones).Select(lighting.Zone).ToList();
        var ok = device.SetZonesEnabled([.. zones.Select(z => z.On)], caps.LedArrayLength);
        ok &= device.SetKeyboardBacklight(KeyboardEffect.Static, backlight.Speed, lighting.Brightness, backlight.Direction, backlight.Color);
        for (var i = 0; i < zones.Count; i++)
        {
            if (zones[i].On)
                ok &= device.SetZoneColor(i + 1, Adjust(RgbColor.FromHex(zones[i].Color)));
        }
        return ok;
    }

    /// <summary>
    /// The firmware's current effect, and each zone's colour and switch where the firmware answers them (else the
    /// model's default colours, all zones lit).
    /// </summary>
    protected override LightingSettings? Read(AcerDevice device)
    {
        if (device.GetKeyboardBacklight() is not { Length: >= 8 } record)
            return null;
        var defaults = new LightingSettings();
        var zoneColors = caps.DefaultZoneColors.Count > 0 ? caps.DefaultZoneColors : [RgbColor.FromHex(defaults.EffectColor)];
        var color = new RgbColor(record[5], record[6], record[7]);
        var zones = Math.Max(caps.Zones, 1);
        var switches = device.GetZonesEnabled(zones);
        return defaults with
        {
            Effect = Enum.IsDefined((KeyboardEffect)record[0]) ? ToLighting((KeyboardEffect)record[0]) : defaults.Effect,
            Brightness = NearestBrightness(record[2]),
            Speed = record[1] is >= KeyboardProtocol.MinSpeed and <= KeyboardProtocol.MaxSpeed ? record[1] : defaults.Speed,
            Direction = Enum.IsDefined((KeyboardDirection)record[4]) ? ToLighting((KeyboardDirection)record[4]) : defaults.Direction,
            EffectColor = color != default ? color.ToHex() : defaults.EffectColor,
            Zones = [.. Enumerable.Range(0, zones).Select(i => new ZoneSetting(
                switches?[i] ?? true,
                // Black counts as not set: a zone is darkened with its switch, not with a black colour.
                (device.GetZoneColor(i + 1) is { } zone && zone != default ? Unadjust(zone) : zoneColors[Math.Min(i, zoneColors.Count - 1)]).ToHex()))],
        };
    }

    /// <summary>NitroSense's per-model colour correction.</summary>
    private RgbColor Adjust(RgbColor color)
    {
        static byte Scale(byte channel, double factor) => (byte)Math.Clamp(Math.Floor(channel * factor), 0, 255);
        var a = caps.ColorAdjust;
        return a.Count < 3 ? color : new RgbColor(Scale(color.R, a[0]), Scale(color.G, a[1]), Scale(color.B, a[2]));
    }

    /// <summary>The colour that <see cref="Adjust"/> turned into <paramref name="color"/>, as near as it can be told.</summary>
    private RgbColor Unadjust(RgbColor color)
    {
        static byte Scale(byte channel, double factor) => factor > 0 ? (byte)Math.Clamp(Math.Round(channel / factor), 0, 255) : channel;
        var a = caps.ColorAdjust;
        return a.Count < 3 ? color : new RgbColor(Scale(color.R, a[0]), Scale(color.G, a[1]), Scale(color.B, a[2]));
    }

    private static LightingEffect ToLighting(KeyboardEffect effect) => effect switch
    {
        KeyboardEffect.Breathing => LightingEffect.Breathing,
        KeyboardEffect.Neon => LightingEffect.Neon,
        KeyboardEffect.Wave => LightingEffect.Wave,
        KeyboardEffect.Shifting => LightingEffect.Shifting,
        KeyboardEffect.Zoom => LightingEffect.Zoom,
        KeyboardEffect.Meteor => LightingEffect.Meteor,
        KeyboardEffect.Twinkling => LightingEffect.Twinkling,
        _ => LightingEffect.Static,
    };

    /// <summary>Null for an effect the keyboard doesn't have.</summary>
    private static KeyboardEffect? ToKeyboard(LightingEffect effect) => effect switch
    {
        LightingEffect.Static => KeyboardEffect.Static,
        LightingEffect.Breathing => KeyboardEffect.Breathing,
        LightingEffect.Neon => KeyboardEffect.Neon,
        LightingEffect.Wave => KeyboardEffect.Wave,
        LightingEffect.Shifting => KeyboardEffect.Shifting,
        LightingEffect.Zoom => KeyboardEffect.Zoom,
        LightingEffect.Meteor => KeyboardEffect.Meteor,
        LightingEffect.Twinkling => KeyboardEffect.Twinkling,
        _ => null,
    };

    private static LightingDirection ToLighting(KeyboardDirection direction) =>
        direction == KeyboardDirection.Left ? LightingDirection.Left : LightingDirection.Right;

    private static KeyboardDirection ToKeyboard(LightingDirection direction) =>
        direction == LightingDirection.Left ? KeyboardDirection.Left : KeyboardDirection.Right;
}
