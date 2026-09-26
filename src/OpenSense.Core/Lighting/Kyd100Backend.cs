using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Lighting;

/// <summary>A light on the embedded controller's lighting HID interface: effects for the whole light, static colours LED by LED.</summary>
public sealed class Kyd100Backend(LightingWorker worker, Kyd100Device device, LightingDeviceInfo info)
    : WorkerLightingBackend(worker, info)
{
    public static string SourceOf(Kyd100Light light) => $"kyd100:{(byte)light:X2}";

    public static LightingDeviceInfo Describe(Kyd100LightInfo light)
    {
        var location = Location(light.Id);
        var maxSpeed = Kyd100Protocol.MaxSpeed(light.Id);
        return new LightingDeviceInfo(location.ToString(), location, LightingBackendKind.Kyd100)
        {
            Zones = Math.Clamp(light.Leds, 1, Kyd100Protocol.MaxZones),
            Effects = [.. Kyd100Protocol.Modes(light.Id, light.Attributes).Select(mode => new EffectTraits(ToLighting(mode), Kyd100Protocol.UsesColor(mode))
            {
                MinSpeed = Kyd100Protocol.MinSpeed,
                MaxSpeed = maxSpeed,
                Directions = Kyd100Protocol.UsesDirection(mode) ? [LightingDirection.Left, LightingDirection.Right] : [],
            })],
            BrightnessLevels = [0, 25, 50, 75, 100],
            Source = SourceOf(light.Id),
        };
    }

    /// <summary>The light id behind a <see cref="LightingDeviceInfo.Source"/>; null for another kind of light.</summary>
    public static Kyd100Light? LightOf(string? source) =>
        source is ['k', 'y', 'd', '1', '0', '0', ':', ..] && byte.TryParse(source.AsSpan(7), System.Globalization.NumberStyles.HexNumber, null, out var id)
            ? (Kyd100Light)id
            : null;

    private Kyd100Light Light => LightOf(Device.Source) ?? Kyd100Light.Keyboard;

    protected override bool Apply(LightingSettings lighting)
    {
        if (lighting.Brightness == 0)
            return device.Update(Light, new Kyd100Update(Kyd100Mode.Off, 0, 0, 0, default, 0));
        if (lighting.Effect == LightingEffect.Static)
        {
            return device.SetStatic(Light, lighting.Brightness,
                [.. Enumerable.Range(0, Device.Zones).Select(i => lighting.Zone(i) is { On: true } zone ? RgbColor.FromHex(zone.Color) : default)]);
        }
        if (ToMode(lighting.Effect) is not { } mode)
            return false;
        var direction = lighting.Direction == LightingDirection.Left ? Kyd100Protocol.DirectionLeft : Kyd100Protocol.DirectionRight;
        var update = mode == Kyd100Mode.FollowOperatingMode
            // The firmware picks the colours; Acer's software sends no brightness or colour with it.
            ? new Kyd100Update(mode, 0, ClampSpeed(lighting.Effect, lighting.Speed), direction, default, 0)
            : new Kyd100Update(mode, lighting.Brightness, ClampSpeed(lighting.Effect, lighting.Speed),
                Kyd100Protocol.UsesDirection(mode) ? direction : 0,
                Kyd100Protocol.UsesColor(mode) ? RgbColor.FromHex(lighting.EffectColor) : default, 0);
        return device.Update(Light, update);
    }

    public static LightingLocation Location(Kyd100Light light) => light switch
    {
        Kyd100Light.Keyboard => LightingLocation.Keyboard,
        Kyd100Light.FrontLightBar => LightingLocation.FrontLightBar,
        Kyd100Light.LeftLightBar => LightingLocation.LeftLightBar,
        Kyd100Light.RightLightBar => LightingLocation.RightLightBar,
        Kyd100Light.RearLightBar => LightingLocation.RearLightBar,
        Kyd100Light.TurboKey => LightingLocation.TurboKey,
        Kyd100Light.ModeKey => LightingLocation.ModeKey,
        Kyd100Light.Logo => LightingLocation.Logo,
        Kyd100Light.CoverLogo => LightingLocation.CoverLogo,
        Kyd100Light.BaseLogo => LightingLocation.BaseLogo,
        _ => LightingLocation.LightBar,
    };

    private static LightingEffect ToLighting(Kyd100Mode mode) => mode switch
    {
        Kyd100Mode.Breathing => LightingEffect.Breathing,
        Kyd100Mode.Neon => LightingEffect.Neon,
        Kyd100Mode.FollowOperatingMode => LightingEffect.FollowOperatingMode,
        Kyd100Mode.Wave => LightingEffect.Wave,
        Kyd100Mode.Shifting => LightingEffect.Shifting,
        Kyd100Mode.Zoom => LightingEffect.Zoom,
        Kyd100Mode.Meteor => LightingEffect.Meteor,
        Kyd100Mode.Twinkling => LightingEffect.Twinkling,
        Kyd100Mode.Combo => LightingEffect.Combo,
        Kyd100Mode.Ripple => LightingEffect.Ripple,
        Kyd100Mode.Snake => LightingEffect.Snake,
        Kyd100Mode.Disco => LightingEffect.Disco,
        _ => LightingEffect.Static,
    };

    private static Kyd100Mode? ToMode(LightingEffect effect) => effect switch
    {
        LightingEffect.Breathing => Kyd100Mode.Breathing,
        LightingEffect.Neon => Kyd100Mode.Neon,
        LightingEffect.FollowOperatingMode => Kyd100Mode.FollowOperatingMode,
        LightingEffect.Wave => Kyd100Mode.Wave,
        LightingEffect.Shifting => Kyd100Mode.Shifting,
        LightingEffect.Zoom => Kyd100Mode.Zoom,
        LightingEffect.Meteor => Kyd100Mode.Meteor,
        LightingEffect.Twinkling => Kyd100Mode.Twinkling,
        LightingEffect.Combo => Kyd100Mode.Combo,
        LightingEffect.Ripple => Kyd100Mode.Ripple,
        LightingEffect.Snake => Kyd100Mode.Snake,
        LightingEffect.Disco => Kyd100Mode.Disco,
        _ => null,
    };
}
