using System.Text.Json.Serialization;

namespace OpenSense.Core.Lighting;

/// <summary>Where a light is. Built-in lights use the name as their settings id: append only.</summary>
public enum LightingLocation
{
    Keyboard,
    LightBar,
    Logo,

    /// <summary>A rear light bar of five or more zones, with effects of its own (Predator Helios 16/18, 2024).</summary>
    InfinityMirror,

    // Lights on the lighting HID interface and USB lights (2024+).
    FrontLightBar,
    LeftLightBar,
    RightLightBar,
    RearLightBar,
    TurboKey,
    ModeKey,

    /// <summary>The logo on the lid.</summary>
    CoverLogo,

    /// <summary>The logo on the base.</summary>
    BaseLogo,

    /// <summary>The MagForce keys' own lights (W, A, S, D).</summary>
    MagKey,

    InfiniteRing,
}

/// <summary>What drives a light.</summary>
public enum LightingBackendKind
{
    /// <summary>The keyboard backlight through the embedded controller (WMI).</summary>
    EcKeyboard,

    /// <summary>Light bars through the embedded controller (WMI).</summary>
    EcLightBar,

    /// <summary>The lid logo through the embedded controller (WMI).</summary>
    EcLogo,

    /// <summary>A light on the embedded controller's lighting HID interface (usage page FF5A).</summary>
    Kyd100,

    /// <summary>A per-key USB keyboard (Chicony, Sunrex).</summary>
    UsbKeyboard,

    /// <summary>The MagForce keys' lights, through the Sunrex keyboard.</summary>
    MagKey,

    /// <summary>A Darfon USB light: cover logo, light bar, InfiniteRing.</summary>
    Darfon,
}

/// <summary>The physical layout of a keyboard, as drawn by the per-key editor.</summary>
public enum KeyboardLayout
{
    /// <summary>US.</summary>
    Ansi,

    /// <summary>UK and most of Europe.</summary>
    Iso,

    /// <summary>Japanese.</summary>
    Jis,
}

/// <summary>What an effect takes on a device.</summary>
public sealed record EffectTraits(LightingEffect Effect, bool Color = false, bool Speed = true)
{
    public int MinSpeed { get; init; } = 1;

    public int MaxSpeed { get; init; } = 9;

    /// <summary>The directions offered; empty when the effect has none.</summary>
    public IReadOnlyList<LightingDirection> Directions { get; init; } = [];

    /// <summary>The light can pick random colours instead of the one set.</summary>
    public bool RandomColor { get; init; }

    [JsonIgnore]
    public bool Direction => Directions.Count > 0;
}

/// <summary>A light OpenSense can drive, as detected.</summary>
/// <param name="Id">Its key in <see cref="LightingConfig.Devices"/>.</param>
public sealed record LightingDeviceInfo(string Id, LightingLocation Location, LightingBackendKind Backend)
{
    /// <summary>Static colour zones (1: one colour for the whole light).</summary>
    public int Zones { get; init; } = 1;

    /// <summary>Zones can be switched off one by one.</summary>
    public bool ZoneSwitches { get; init; }

    /// <summary>The effects besides static colours, in the order Acer's software lists them.</summary>
    public IReadOnlyList<EffectTraits> Effects { get; init; } = [];

    /// <summary>Brightness steps (0..100); the first may be 0, off.</summary>
    public IReadOnlyList<int> BrightnessLevels { get; init; } = [0, 25, 50, 75, 100];

    /// <summary>
    /// The keys with a light of their own, for <see cref="LightingEffect.PerKey"/>: W3C <c>KeyboardEvent.code</c> names,
    /// plus <c>Fn</c>, <c>IntlHash</c> (the ISO # and Japanese む key, left of Enter), <c>PredatorSense</c>,
    /// <c>MyKey</c> and <c>Power</c>.
    /// </summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>The keyboard's layout as Acer's software knows it; null when it doesn't say.</summary>
    public KeyboardLayout? Layout { get; init; }

    /// <summary>Where the engine finds a HID or USB light again (e.g. <c>kyd100:21</c>); null for the embedded controller's.</summary>
    public string? Source { get; init; }

    public EffectTraits? Traits(LightingEffect effect) => Effects.FirstOrDefault(e => e.Effect == effect);

    public bool Offers(LightingEffect effect) => effect == LightingEffect.Static || Traits(effect) is not null;
}
