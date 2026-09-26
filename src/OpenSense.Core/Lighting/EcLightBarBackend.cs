using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

/// <summary>
/// The light bars on the embedded controller, as one light: effects and brightness reach every bar at once, so only the
/// static colours are per zone (the bars' zones one after another, front to rear). A rear bar of five or more zones
/// makes it the Infinity Mirror, with that one's effects.
/// </summary>
public sealed class EcLightBarBackend(IDeviceDispatcher dispatcher, IReadOnlyList<LightBar> bars, int ledArrayLength)
    : EcLightingBackend(dispatcher, Describe(bars, ledArrayLength))
{
    public static LightingDeviceInfo Describe(IReadOnlyList<LightBar> bars, int ledArrayLength)
    {
        var mirror = bars.Any(b => b.Id == LightBarId.Rear && b.Zones >= LightBarProtocol.InfinityMirrorZones);
        var location = mirror ? LightingLocation.InfinityMirror : LightingLocation.LightBar;
        return new LightingDeviceInfo(location.ToString(), location, LightingBackendKind.EcLightBar)
        {
            Zones = Math.Max(1, bars.Sum(b => b.Zones)),
            // Before interface 2.86 Acer's software only ever sends a fixed "front zones on" value.
            ZoneSwitches = ledArrayLength > 8,
            Effects = [.. (mirror ? LightBarProtocol.InfinityMirrorEffects : LightBarProtocol.BarEffects).Select(Traits)],
            BrightnessLevels = [0, 25, 50, 75, 100],
        };
    }

    private static EffectTraits Traits(LightBarEffect effect) => new(ToLighting(effect), LightBarProtocol.UsesColor(effect))
    {
        MinSpeed = LightBarProtocol.MinSpeed,
        MaxSpeed = LightBarProtocol.MaxSpeed,
        Directions = LightBarProtocol.UsesDirection(effect) ? [LightingDirection.Left, LightingDirection.Right] : [],
    };

    protected override bool Apply(AcerDevice device, LightingSettings lighting)
    {
        if (!Device.Offers(lighting.Effect) || ToLightBar(lighting.Effect) is not { } effect)
            return false;
        var direction = lighting.Direction == LightingDirection.Left ? LightBarProtocol.DirectionLeft : LightBarProtocol.DirectionRight;
        if (effect != LightBarEffect.Static)
        {
            return device.SetLightBarBacklight(effect, ClampSpeed(lighting.Effect, lighting.Speed), lighting.Brightness, direction,
                RgbColor.FromHex(lighting.EffectColor));
        }

        // As Acer's software does it: zones on and off, the brightness, then each zone's colour.
        var ok = true;
        var first = 0;
        List<(LightBarId Bar, int Zone, ZoneSetting Setting)> zones = [];
        foreach (var bar in bars)
        {
            var settings = Enumerable.Range(first, bar.Zones).Select(i => lighting.Zone(i)).ToList();
            ok &= device.SetLightBarZones(bar.Id, [.. settings.Select(z => z.On || !Device.ZoneSwitches)], ledArrayLength);
            zones.AddRange(settings.Select((z, i) => (bar.Id, i + 1, z)));
            first += bar.Zones;
        }
        ok &= device.SetLightBarBacklight(LightBarEffect.Static, 0, lighting.Brightness, 0, default);
        foreach (var (bar, zone, setting) in zones)
        {
            if (setting.On || !Device.ZoneSwitches)
                ok &= device.SetLightBarZoneColor(bar, zone, RgbColor.FromHex(setting.Color));
        }
        return ok;
    }

    /// <summary>Nothing reads the light bars back.</summary>
    protected override LightingSettings? Read(AcerDevice device) => null;

    private static LightingEffect ToLighting(LightBarEffect effect) => effect switch
    {
        LightBarEffect.Breathing => LightingEffect.Breathing,
        LightBarEffect.Neon => LightingEffect.Neon,
        LightBarEffect.Wave => LightingEffect.Wave,
        LightBarEffect.Twinkling => LightingEffect.Twinkling,
        LightBarEffect.Snake => LightingEffect.Snake,
        LightBarEffect.Lightning => LightingEffect.Lightning,
        LightBarEffect.Stack => LightingEffect.Stack,
        LightBarEffect.MotionPoint => LightingEffect.MotionPoint,
        LightBarEffect.ZoomIn => LightingEffect.ZoomIn,
        _ => LightingEffect.Static,
    };

    /// <summary>Null for an effect the light bars don't have.</summary>
    private static LightBarEffect? ToLightBar(LightingEffect effect) => effect switch
    {
        LightingEffect.Static => LightBarEffect.Static,
        LightingEffect.Breathing => LightBarEffect.Breathing,
        LightingEffect.Neon => LightBarEffect.Neon,
        LightingEffect.Wave => LightBarEffect.Wave,
        LightingEffect.Twinkling => LightBarEffect.Twinkling,
        LightingEffect.Snake => LightBarEffect.Snake,
        LightingEffect.Lightning => LightBarEffect.Lightning,
        LightingEffect.Stack => LightBarEffect.Stack,
        LightingEffect.MotionPoint => LightBarEffect.MotionPoint,
        LightingEffect.ZoomIn => LightBarEffect.ZoomIn,
        _ => null,
    };
}
