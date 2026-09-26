using OpenSense.Core.Control;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

/// <summary>The lid logo on the embedded controller: one colour, Breathing or Neon, brightness 0 is off.</summary>
public sealed class EcLogoBackend(IDeviceDispatcher dispatcher) : EcLightingBackend(dispatcher, Describe())
{
    public const string Id = nameof(LightingLocation.Logo);

    public static LightingDeviceInfo Describe() => new(Id, LightingLocation.Logo, LightingBackendKind.EcLogo)
    {
        Zones = 1,
        Effects = [.. LogoProtocol.Effects.Select(b => new EffectTraits(ToLighting(b), LogoProtocol.UsesColor(b))
        {
            MinSpeed = LogoProtocol.MinSpeed,
            MaxSpeed = LogoProtocol.MaxSpeed,
        })],
        BrightnessLevels = [0, 25, 50, 75, 100],
    };

    protected override bool Apply(AcerDevice device, LightingSettings lighting)
    {
        var speed = ClampSpeed(lighting.Effect, lighting.Speed);
        return lighting.Effect switch
        {
            LightingEffect.Static => device.SetLogo(LogoBehavior.Static, RgbColor.FromHex(lighting.Zone(0).Color), lighting.Brightness,
                Math.Clamp(speed, LogoProtocol.MinSpeed, LogoProtocol.MaxSpeed)),
            LightingEffect.Breathing => device.SetLogo(LogoBehavior.Breathing, RgbColor.FromHex(lighting.EffectColor), lighting.Brightness, speed),
            LightingEffect.Neon => device.SetLogo(LogoBehavior.Neon, default, lighting.Brightness, speed),
            _ => false,
        };
    }

    /// <summary>Nothing reads the logo back.</summary>
    protected override LightingSettings? Read(AcerDevice device) => null;

    private static LightingEffect ToLighting(LogoBehavior behavior) => behavior switch
    {
        LogoBehavior.Breathing => LightingEffect.Breathing,
        LogoBehavior.Neon => LightingEffect.Neon,
        _ => LightingEffect.Static,
    };
}
