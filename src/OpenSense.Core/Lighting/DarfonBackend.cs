using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Lighting;

/// <summary>A Darfon light: the cover logo (three static colours), the light bar or the InfiniteRing (one).</summary>
public sealed class DarfonBackend(LightingWorker worker, DarfonDevice device, DarfonPart part, LightingDeviceInfo info)
    : WorkerLightingBackend(worker, info)
{
    public static string SourceOf(ushort productId, DarfonPart part) => $"darfon:{productId:X4}:{part}";

    public static LightingDeviceInfo Describe(DarfonModel model, DarfonPart part)
    {
        var location = part switch
        {
            DarfonPart.CoverLogo => LightingLocation.CoverLogo,
            DarfonPart.LightBar => LightingLocation.LightBar,
            _ => LightingLocation.InfiniteRing,
        };
        return new LightingDeviceInfo(location.ToString(), location, LightingBackendKind.Darfon)
        {
            Zones = part == DarfonPart.CoverLogo ? DarfonProtocol.LogoLeds : 1,
            Effects = [.. DarfonProtocol.Effects(part, model.Model2025).Select(effect => new EffectTraits(ToLighting(effect), DarfonProtocol.UsesColor(effect))
            {
                MinSpeed = DarfonProtocol.MinSpeed,
                MaxSpeed = DarfonProtocol.MaxSpeed,
                Directions = DarfonProtocol.UsesDirection(effect) ? [LightingDirection.Left, LightingDirection.Right] : [],
                RandomColor = DarfonProtocol.TakesRandomColor(effect),
            })],
            BrightnessLevels = [0, 25, 50, 75, 100],
            Source = SourceOf(model.ProductId, part),
        };
    }

    protected override bool Apply(LightingSettings lighting)
    {
        if (lighting.Brightness == 0)
            return device.Send(DarfonProtocol.OffSequence(part));
        if (lighting.Effect == LightingEffect.Static)
        {
            var colors = Enumerable.Range(0, Device.Zones)
                .Select(i => lighting.Zone(i) is { On: true } zone ? RgbColor.FromHex(zone.Color) : default)
                .ToList();
            return part == DarfonPart.CoverLogo
                ? device.Send(DarfonProtocol.LogoStaticSequence(lighting.Brightness, colors))
                : device.SendSteps(DarfonProtocol.BarStaticSteps(part, lighting.Brightness, colors[0]));
        }
        if (ToEffect(lighting.Effect) is not { } effect)
            return false;
        var direction = lighting.Direction == LightingDirection.Left ? UsbKeyboardProtocol.DirectionLeft : UsbKeyboardProtocol.DirectionRight;
        return device.Send(DarfonProtocol.EffectSequence(part, effect, ClampSpeed(lighting.Effect, lighting.Speed), lighting.Brightness,
            direction, RgbColor.FromHex(lighting.EffectColor), Random(lighting)));
    }

    private static LightingEffect ToLighting(DarfonEffect effect) => effect switch
    {
        DarfonEffect.Breathing => LightingEffect.Breathing,
        DarfonEffect.LogoWave => LightingEffect.Wave,
        DarfonEffect.LogoSnake or DarfonEffect.Snake => LightingEffect.Snake,
        DarfonEffect.LogoSwiping or DarfonEffect.Swiping => LightingEffect.Swiping,
        DarfonEffect.LogoNeon => LightingEffect.Neon,
        DarfonEffect.Dazzling => LightingEffect.Dazzling,
        DarfonEffect.Ripple => LightingEffect.Ripple,
        DarfonEffect.RowWave => LightingEffect.RowWave,
        DarfonEffect.Racing => LightingEffect.Racing,
        DarfonEffect.Disco => LightingEffect.Disco,
        _ => LightingEffect.Static,
    };

    private DarfonEffect? ToEffect(LightingEffect effect) => (part == DarfonPart.CoverLogo, effect) switch
    {
        (_, LightingEffect.Breathing) => DarfonEffect.Breathing,
        (true, LightingEffect.Wave) => DarfonEffect.LogoWave,
        (true, LightingEffect.Snake) => DarfonEffect.LogoSnake,
        (true, LightingEffect.Swiping) => DarfonEffect.LogoSwiping,
        (true, LightingEffect.Neon) => DarfonEffect.LogoNeon,
        (false, LightingEffect.Snake) => DarfonEffect.Snake,
        (false, LightingEffect.Swiping) => DarfonEffect.Swiping,
        (false, LightingEffect.Dazzling) => DarfonEffect.Dazzling,
        (false, LightingEffect.Ripple) => DarfonEffect.Ripple,
        (false, LightingEffect.RowWave) => DarfonEffect.RowWave,
        (false, LightingEffect.Racing) => DarfonEffect.Racing,
        (false, LightingEffect.Disco) => DarfonEffect.Disco,
        _ => null,
    };
}
