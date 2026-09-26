using OpenSense.Core.Hardware;
using OpenSense.Core.Hardware.Hid;

namespace OpenSense.Core.Lighting;

/// <summary>A per-key USB keyboard: one static colour, effects, or a colour per key.</summary>
public sealed class UsbKeyboardBackend(LightingWorker worker, UsbKeyboardDevice keyboard, LightingDeviceInfo info)
    : WorkerLightingBackend(worker, info)
{
    public const string SourceName = "usb-keyboard";

    public static LightingDeviceInfo Describe(UsbKeyboardDevice keyboard)
    {
        var model = keyboard.Model;
        var sunrex = model.Maker == UsbKeyboardMaker.Sunrex;
        List<EffectTraits> effects = [.. UsbKeyboardProtocol.Effects(model.Generation).Select(effect => new EffectTraits(ToLighting(effect), UsbKeyboardProtocol.UsesColor(effect))
        {
            MinSpeed = UsbKeyboardProtocol.MinSpeed,
            MaxSpeed = UsbKeyboardProtocol.MaxSpeed,
            Directions = effect == UsbKeyboardEffect.Wave
                ? [LightingDirection.Left, LightingDirection.Right, LightingDirection.Up, LightingDirection.Down]
                : [],
            RandomColor = sunrex && UsbKeyboardProtocol.TakesRandomColor(effect),
        })];
        if (keyboard.Leds is { Count: > 0 })
            effects.Insert(0, new EffectTraits(LightingEffect.PerKey, Speed: false));
        return new LightingDeviceInfo(nameof(LightingLocation.Keyboard), LightingLocation.Keyboard, LightingBackendKind.UsbKeyboard)
        {
            Zones = 1,
            Effects = effects,
            BrightnessLevels = [0, 25, 50, 75, 100],
            Keys = keyboard.Leds is { } leds ? [.. leds.OrderBy(l => l.Value).Select(l => l.Key)] : [],
            Layout = model.Layout,
            Source = SourceName,
        };
    }

    protected override bool Apply(LightingSettings lighting)
    {
        var model = keyboard.Model;
        if (lighting.Brightness == 0)
            return keyboard.Send(UsbKeyboardProtocol.OffSequence(model));
        if (lighting.Effect == LightingEffect.PerKey)
        {
            if (keyboard.Leds is not { } leds)
                return false;
            var colors = new Dictionary<int, RgbColor>();
            foreach (var (key, led) in leds)
            {
                if (lighting.KeyColor(key) is { } hex)
                    colors[led] = RgbColor.FromHex(hex);
            }
            return keyboard.Upload(colors, lighting.Brightness);
        }
        var direction = lighting.Direction switch
        {
            LightingDirection.Left => UsbKeyboardProtocol.DirectionLeft,
            LightingDirection.Up => UsbKeyboardProtocol.DirectionUp,
            LightingDirection.Down => UsbKeyboardProtocol.DirectionDown,
            _ => UsbKeyboardProtocol.DirectionRight,
        };
        var (effect, color) = lighting.Effect == LightingEffect.Static
            ? (UsbKeyboardEffect.Static, lighting.Zone(0).Color)
            : (ToEffect(lighting.Effect), lighting.EffectColor);
        if (effect is not { } value)
            return false;
        return keyboard.Send(UsbKeyboardProtocol.EffectSequence(model, value, ClampSpeed(lighting.Effect, lighting.Speed), lighting.Brightness,
            direction, RgbColor.FromHex(color), Random(lighting)));
    }

    private static LightingEffect ToLighting(UsbKeyboardEffect effect) => effect switch
    {
        UsbKeyboardEffect.Breathing => LightingEffect.Breathing,
        UsbKeyboardEffect.Wave => LightingEffect.Wave,
        UsbKeyboardEffect.Snake => LightingEffect.Snake,
        UsbKeyboardEffect.Ripple => LightingEffect.Ripple,
        UsbKeyboardEffect.Neon => LightingEffect.Neon,
        UsbKeyboardEffect.Raindrop => LightingEffect.Raindrop,
        UsbKeyboardEffect.Lightning => LightingEffect.Lightning,
        UsbKeyboardEffect.FollowOperatingMode => LightingEffect.FollowOperatingMode,
        UsbKeyboardEffect.Fireball => LightingEffect.Fireball,
        UsbKeyboardEffect.Snow => LightingEffect.Snow,
        UsbKeyboardEffect.Heartbeat => LightingEffect.Heartbeat,
        UsbKeyboardEffect.Dazzling => LightingEffect.Dazzling,
        UsbKeyboardEffect.Matrix => LightingEffect.Matrix,
        UsbKeyboardEffect.Swiping => LightingEffect.Swiping,
        UsbKeyboardEffect.Racing => LightingEffect.Racing,
        UsbKeyboardEffect.Sprouting => LightingEffect.Sprouting,
        UsbKeyboardEffect.Disco => LightingEffect.Disco,
        UsbKeyboardEffect.PingPong => LightingEffect.PingPong,
        UsbKeyboardEffect.RowWave => LightingEffect.RowWave,
        UsbKeyboardEffect.LightShow => LightingEffect.LightShow,
        _ => LightingEffect.Static,
    };

    private static UsbKeyboardEffect? ToEffect(LightingEffect effect) => effect switch
    {
        LightingEffect.Breathing => UsbKeyboardEffect.Breathing,
        LightingEffect.Wave => UsbKeyboardEffect.Wave,
        LightingEffect.Snake => UsbKeyboardEffect.Snake,
        LightingEffect.Ripple => UsbKeyboardEffect.Ripple,
        LightingEffect.Neon => UsbKeyboardEffect.Neon,
        LightingEffect.Raindrop => UsbKeyboardEffect.Raindrop,
        LightingEffect.Lightning => UsbKeyboardEffect.Lightning,
        LightingEffect.FollowOperatingMode => UsbKeyboardEffect.FollowOperatingMode,
        LightingEffect.Fireball => UsbKeyboardEffect.Fireball,
        LightingEffect.Snow => UsbKeyboardEffect.Snow,
        LightingEffect.Heartbeat => UsbKeyboardEffect.Heartbeat,
        LightingEffect.Dazzling => UsbKeyboardEffect.Dazzling,
        LightingEffect.Matrix => UsbKeyboardEffect.Matrix,
        LightingEffect.Swiping => UsbKeyboardEffect.Swiping,
        LightingEffect.Racing => UsbKeyboardEffect.Racing,
        LightingEffect.Sprouting => UsbKeyboardEffect.Sprouting,
        LightingEffect.Disco => UsbKeyboardEffect.Disco,
        LightingEffect.PingPong => UsbKeyboardEffect.PingPong,
        LightingEffect.RowWave => UsbKeyboardEffect.RowWave,
        LightingEffect.LightShow => UsbKeyboardEffect.LightShow,
        _ => null,
    };
}
