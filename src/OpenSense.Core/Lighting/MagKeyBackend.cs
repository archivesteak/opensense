using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Lighting;

/// <summary>The MagForce keys' own lights (W, A, S, D), through the Sunrex keyboard they sit in.</summary>
public sealed class MagKeyBackend(LightingWorker worker, UsbKeyboardDevice keyboard, LightingDeviceInfo info)
    : WorkerLightingBackend(worker, info)
{
    public const string SourceName = "magkey";

    /// <param name="model2025">The 2025 models' effect list (PH16-73, PH18-73) rather than 2024's.</param>
    public static LightingDeviceInfo Describe(bool model2025) => new(nameof(LightingLocation.MagKey), LightingLocation.MagKey, LightingBackendKind.MagKey)
    {
        Zones = 1,
        Effects =
        [
            new EffectTraits(LightingEffect.PerKey, Speed: false),
            .. UsbKeyboardProtocol.MagKeyEffects(model2025).Select(effect => new EffectTraits(ToLighting(effect), UsbKeyboardProtocol.UsesColor(effect))
            {
                MinSpeed = UsbKeyboardProtocol.MinSpeed,
                MaxSpeed = UsbKeyboardProtocol.MaxSpeed,
                Directions = effect == MagKeyEffect.Wave
                    ? [LightingDirection.Left, LightingDirection.Right, LightingDirection.Up, LightingDirection.Down]
                    : [],
                RandomColor = UsbKeyboardProtocol.TakesRandomColor(effect),
            }),
        ],
        BrightnessLevels = [0, 25, 50, 75, 100],
        Keys = UsbKeyboardProtocol.MagKeys,
        Source = SourceName,
    };

    protected override bool Apply(LightingSettings lighting)
    {
        var model = keyboard.Model;
        var direction = lighting.Direction switch
        {
            LightingDirection.Left => UsbKeyboardProtocol.DirectionLeft,
            LightingDirection.Up => UsbKeyboardProtocol.DirectionUp,
            LightingDirection.Down => UsbKeyboardProtocol.DirectionDown,
            _ => UsbKeyboardProtocol.DirectionRight,
        };
        if (lighting.Brightness == 0)
            return keyboard.Send(UsbKeyboardProtocol.MagKeySequence(model, MagKeyEffect.Off, lighting.Speed, 0, direction, default, false));
        if (lighting.Effect == LightingEffect.PerKey)
        {
            return keyboard.UploadMagKeys(
                [.. UsbKeyboardProtocol.MagKeys.Select(k => lighting.KeyColor(k) is { } color ? RgbColor.FromHex(color) : default)],
                lighting.Brightness);
        }
        var (effect, color) = lighting.Effect == LightingEffect.Static
            ? (MagKeyEffect.Static, lighting.Zone(0).Color)
            : (ToEffect(lighting.Effect), lighting.EffectColor);
        if (effect is not { } value)
            return false;
        return keyboard.Send(UsbKeyboardProtocol.MagKeySequence(model, value, ClampSpeed(lighting.Effect, lighting.Speed), lighting.Brightness,
            direction, RgbColor.FromHex(color), Random(lighting)));
    }

    private static LightingEffect ToLighting(MagKeyEffect effect) => effect switch
    {
        MagKeyEffect.Breathing => LightingEffect.Breathing,
        MagKeyEffect.Wave => LightingEffect.Wave,
        MagKeyEffect.Snake => LightingEffect.Snake,
        MagKeyEffect.Neon => LightingEffect.Neon,
        MagKeyEffect.Star => LightingEffect.Star,
        MagKeyEffect.Rainbow => LightingEffect.Rainbow,
        MagKeyEffect.Slash => LightingEffect.Slash,
        MagKeyEffect.Blasting => LightingEffect.Blasting,
        MagKeyEffect.RowWave => LightingEffect.RowWave,
        MagKeyEffect.Swiping => LightingEffect.Swiping,
        MagKeyEffect.PerKey => LightingEffect.PerKey,
        _ => LightingEffect.Static,
    };

    private static MagKeyEffect? ToEffect(LightingEffect effect) => effect switch
    {
        LightingEffect.Breathing => MagKeyEffect.Breathing,
        LightingEffect.Wave => MagKeyEffect.Wave,
        LightingEffect.Snake => MagKeyEffect.Snake,
        LightingEffect.Neon => MagKeyEffect.Neon,
        LightingEffect.Star => MagKeyEffect.Star,
        LightingEffect.Rainbow => MagKeyEffect.Rainbow,
        LightingEffect.Slash => MagKeyEffect.Slash,
        LightingEffect.Blasting => MagKeyEffect.Blasting,
        LightingEffect.RowWave => MagKeyEffect.RowWave,
        LightingEffect.Swiping => MagKeyEffect.Swiping,
        _ => null,
    };
}
