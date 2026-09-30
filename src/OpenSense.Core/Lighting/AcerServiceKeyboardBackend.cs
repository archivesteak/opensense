using System.Text.Json.Nodes;
using OpenSense.Core.Hardware;

namespace OpenSense.Core.Lighting;

/// <summary>
/// The keyboard's RGB through AcerService's local JSON API, for laptops where the legacy ACPI-WMI keyboard calls
/// (<see cref="EcKeyboardBackend"/>) don't reach it (2024+ Predators: confirmed on the Helios Neo 16, PHN16-72).
/// AcerLightingService must be running; <see cref="AcerServiceTransport.IsAvailable"/> gates whether this backend is
/// offered at all, so it never appears instead of a working native path.
/// </summary>
public sealed class AcerServiceKeyboardBackend(LightingDeviceInfo device) : LightingBackendBase(device), ILightingBackend
{
    public const string Id = nameof(LightingLocation.Keyboard);

    private const int ServiceSteps = 5;

    /// <summary>Effect names AcerService accepts for the keyboard (device 0), by <see cref="KeyboardEffect"/> position.</summary>
    private static readonly string[] EffectNames = ["STATIC", "BREATHING", "NEON", "WAVE", "SHIFTING", "ZOOM", "METEOR", "TWINKLING"];

    public static LightingDeviceInfo Describe() => new(Id, LightingLocation.Keyboard, LightingBackendKind.AcerServiceKeyboard)
    {
        Zones = 4,
        ZoneSwitches = true,
        Effects = [.. KeyboardProtocol.ZonedEffects.Select(Traits)],
        BrightnessLevels = KeyboardProtocol.BrightnessLevels,
    };

    private static EffectTraits Traits(KeyboardEffect effect) => new(ToLighting(effect), KeyboardProtocol.UsesColor(effect))
    {
        MinSpeed = KeyboardProtocol.MinSpeed,
        MaxSpeed = KeyboardProtocol.MaxSpeed,
        Directions = KeyboardProtocol.UsesDirection(effect) ? [LightingDirection.Left, LightingDirection.Right] : [],
    };

    public Task<bool> ApplyAsync(LightingSettings settings) => Task.Run(() => Apply(settings));

    /// <summary>AcerService doesn't expose a way to read the per-zone state back; only the global effect/colour.</summary>
    public Task<LightingSettings?> ReadAsync() => Task.Run(Read);

    private bool Apply(LightingSettings lighting)
    {
        if (!Device.Offers(lighting.Effect))
            return false;
        if (lighting.Effect != LightingEffect.Static)
            return SetGlobal(lighting);

        var zones = Enumerable.Range(0, Device.Zones).Select(lighting.Zone).ToList();
        var parameters = new JsonObject
        {
            ["device"] = 1,
            ["effect"] = "STATIC",
            ["brightness"] = ToServiceBrightness(lighting.Brightness),
            ["colortype"] = 1,
            ["LEDs"] = new JsonArray([.. zones.Select((z, i) => (JsonNode)new JsonObject
            {
                ["LED_id"] = i,
                ["color"] = z.Color.ToLowerInvariant(),
                ["status"] = z.On ? 1 : 0,
            })]),
        };
        return AcerServiceTransport.SetDeviceData("LIGHTING", parameters);
    }

    private static bool SetGlobal(LightingSettings lighting)
    {
        var effect = ToKeyboard(lighting.Effect) is { } e ? EffectNames[(int)e] : EffectNames[0];
        var parameters = new JsonObject
        {
            ["device"] = 0,
            ["effect"] = effect,
            ["speed"] = ToServiceSpeed(lighting.Speed),
            // Untested: Static (the only effect proven end-to-end so far) ignores direction, so which way round
            // AcerService's own 0/1 goes for Wave/Shifting hasn't been confirmed against real hardware yet.
            ["direction"] = lighting.Direction == LightingDirection.Left ? 0 : 1,
            ["brightness"] = ToServiceBrightness(lighting.Brightness),
            ["color"] = lighting.EffectColor.ToLowerInvariant(),
            ["colortype"] = 1,
            ["subindex"] = new JsonObject { ["1"] = effect, ["2"] = effect },
        };
        return AcerServiceTransport.SetDeviceData("LIGHTING", parameters);
    }

    private LightingSettings? Read()
    {
        if (AcerServiceTransport.Query("LIGHTING") is not JsonObject data)
            return null;
        var defaults = LightingSettings.For(Device);
        var effect = data["effect"]?.ToString() is { } name && Array.IndexOf(EffectNames, name) is >= 0 and var i
            ? ToLighting((KeyboardEffect)i)
            : defaults.Effect;
        return defaults with
        {
            Effect = effect,
            Brightness = data["brightness"]?.GetValue<int>() is { } b ? FromServiceBrightness(b) : defaults.Brightness,
            Speed = data["speed"]?.GetValue<int>() is { } s ? FromServiceSpeed(s) : defaults.Speed,
            Direction = data["direction"]?.GetValue<int>() == 0 ? LightingDirection.Left : LightingDirection.Right,
            EffectColor = data["color"]?.ToString() is { Length: > 0 } c ? (c.StartsWith('#') ? c : $"#{c}") : defaults.EffectColor,
        };
    }

    private static int ToServiceBrightness(int percent) => Math.Clamp((int)Math.Round(percent / 100.0 * ServiceSteps), 1, ServiceSteps);

    private static int FromServiceBrightness(int step) => Math.Clamp(step * 100 / ServiceSteps, 0, 100);

    private static int ToServiceSpeed(int speed) =>
        Math.Clamp((int)Math.Round((speed - KeyboardProtocol.MinSpeed) / (double)(KeyboardProtocol.MaxSpeed - KeyboardProtocol.MinSpeed) * (ServiceSteps - 1)) + 1, 1, ServiceSteps);

    private static int FromServiceSpeed(int step) =>
        KeyboardProtocol.MinSpeed + (int)Math.Round((step - 1) / (double)(ServiceSteps - 1) * (KeyboardProtocol.MaxSpeed - KeyboardProtocol.MinSpeed));

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
}
