namespace OpenSense.Core.Hardware;

/// <summary>Keyboard backlight effects understood by <c>SetGamingKBBacklight</c>. Values are the firmware encoding.</summary>
public enum KeyboardEffect : byte
{
    Static = 0,
    Breathing = 1,
    Neon = 2,
    Wave = 3,
    Shifting = 4,
    Zoom = 5,
    Meteor = 6,
    Twinkling = 7,
}

/// <summary>
/// Wave's and Shifting's direction byte, as NitroSense and PredatorSense name it. The controller only tells 1 from
/// the rest, so a zoned keyboard has no up or down.
/// </summary>
public enum KeyboardDirection : byte
{
    Right = 1,
    Left = 2,
}

/// <summary>
/// How Acer's software for the model lays out <c>SetGamingKBBacklight</c> (<see cref="KeyboardProtocol.BacklightPayload"/>).
/// </summary>
public enum KeyboardPayloadLayout
{
    /// <summary>NitroSense 3.01, on the Nitros whose own software it is (<see cref="AcerModelProfile.KeyboardLayout"/>).</summary>
    NitroSense,

    /// <summary>PredatorSense 5.1, the same on every model Acer's current software drives.</summary>
    PredatorSense,
}

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor White { get; } = new(255, 255, 255);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary><c>#RRGGBB</c> (the # is optional); white for anything else, so a damaged saved colour can't stop the lighting.</summary>
    public static RgbColor FromHex(string? hex)
    {
        var s = (hex ?? "").TrimStart('#');
        return s.Length == 6 && uint.TryParse(s, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var rgb)
            ? new RgbColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : White;
    }
}

/// <summary>Bit-level encoding of keyboard lighting and keyboard/display settings.</summary>
public static class KeyboardProtocol
{
    public const int MinSpeed = 1;
    public const int MaxSpeed = 9;

    /// <summary>NitroSense offers five brightness steps; the firmware takes 0..100.</summary>
    public static IReadOnlyList<int> BrightnessLevels { get; } = [0, 25, 50, 75, 100];

    /// <summary>The effects of a zoned keyboard (three or four zones), in the order Acer's software lists them.</summary>
    public static IReadOnlyList<KeyboardEffect> ZonedEffects { get; } =
    [
        KeyboardEffect.Breathing, KeyboardEffect.Neon, KeyboardEffect.Wave, KeyboardEffect.Shifting, KeyboardEffect.Zoom,
        KeyboardEffect.Meteor, KeyboardEffect.Twinkling,
    ];

    /// <summary>
    /// The effects of an embedded controller that has no Meteor or Twinkling (AN515-45 V1.13: its effect code handles 1–5,
    /// and 6 or 7 leaves the keyboard dark), as NitroSense offers them.
    /// </summary>
    public static IReadOnlyList<KeyboardEffect> FiveZonedEffects { get; } =
    [
        KeyboardEffect.Breathing, KeyboardEffect.Neon, KeyboardEffect.Wave, KeyboardEffect.Shifting, KeyboardEffect.Zoom,
    ];

    /// <summary>Neon and Wave are rainbow effects: the firmware draws its own colours.</summary>
    public static bool UsesColor(KeyboardEffect effect) => effect is not (KeyboardEffect.Static or KeyboardEffect.Neon or KeyboardEffect.Wave);

    public static bool UsesDirection(KeyboardEffect effect) => effect is KeyboardEffect.Wave or KeyboardEffect.Shifting;

    public static bool UsesSpeed(KeyboardEffect effect) => effect != KeyboardEffect.Static;

    /// <summary>
    /// The 16-byte <c>SetGamingKBBacklight</c> payload as Acer's software for the model sends it: an 8-byte record (effect,
    /// speed, brightness, flags, direction, R, G, B), then more.
    /// <list type="bullet">
    /// <item>NitroSense repeats the record, with byte 9 set to 1, and flags Wave with <c>0x08</c> in byte 3. It has
    /// effects 0-5; Meteor and Twinkling get PredatorSense's tail, without which Twinkling stays dark (AN515-57).</item>
    /// <item>PredatorSense sends <c>03 01</c> and zeros after the record, nothing in byte 3, and the direction with
    /// every effect but static.</item>
    /// </list>
    /// </summary>
    public static byte[] BacklightPayload(KeyboardEffect effect, int speed, int brightness, KeyboardDirection direction, RgbColor color,
        KeyboardPayloadLayout layout = KeyboardPayloadLayout.NitroSense)
    {
        var predator = layout == KeyboardPayloadLayout.PredatorSense;
        var directionByte = predator ? effect != KeyboardEffect.Static : UsesDirection(effect);
        Span<byte> record =
        [
            (byte)effect,
            (byte)(UsesSpeed(effect) ? Math.Clamp(speed, MinSpeed, MaxSpeed) : 0),
            (byte)Math.Clamp(brightness, 0, 100),
            (byte)(effect == KeyboardEffect.Wave && !predator ? 0x08 : 0),
            (byte)(directionByte ? direction : 0),
            UsesColor(effect) ? color.R : (byte)0,
            UsesColor(effect) ? color.G : (byte)0,
            UsesColor(effect) ? color.B : (byte)0,
        ];
        var payload = new byte[16];
        record.CopyTo(payload);
        if (predator || effect is KeyboardEffect.Meteor or KeyboardEffect.Twinkling)
        {
            payload[8] = 0x03;
        }
        else
        {
            record.CopyTo(payload.AsSpan(8));
        }
        payload[9] = 1;
        return payload;
    }

    /// <summary>
    /// NitroSense's layout on the Nitros whose own software is NitroSense (<see cref="AcerModelProfile.KeyboardLayout"/>),
    /// PredatorSense's on every other model, and NitroSense's when the model can't be read. What is installed on the
    /// laptop plays no part.
    /// </summary>
    public static KeyboardPayloadLayout PayloadLayout(string? model) =>
        model is null ? KeyboardPayloadLayout.NitroSense : AcerModelProfile.For(model).KeyboardLayout ?? KeyboardPayloadLayout.PredatorSense;

    /// <summary><c>SetGamingRgbKb</c>: zone 1..4 and its colour.</summary>
    public static ulong ZoneColorInput(int zone, RgbColor color) =>
        (1UL << (zone - 1)) | ((ulong)color.R << 8) | ((ulong)color.G << 16) | ((ulong)color.B << 24);

    /// <summary><c>GetGamingRgbKb</c> takes the zone's bit, as the setter does.</summary>
    public static uint ZoneColorQuery(int zone) => 1u << (zone - 1);

    /// <summary>The colour in a <c>GetGamingRgbKb</c> answer: R, G, B in bytes 1-3 (AN515-57: <c>0x552DFF00</c> is #FF2D55).</summary>
    public static RgbColor ZoneColorValue(ulong output) => new((byte)(output >> 8), (byte)(output >> 16), (byte)(output >> 24));

    /// <summary><c>GetGamingLED</c> with the keyboard's group answers the zones' switches as the setter takes them.</summary>
    public const uint ZoneEnableQuery = 0x08;

    /// <summary>Which zones a <see cref="ZoneEnableQuery"/> answer has lit: bit 40 + n for zone n + 1 (AN515-57, all four: <c>0xF0000000000</c>).</summary>
    public static IReadOnlyList<bool> ZoneEnableValue(ulong output, int zones) =>
        [.. Enumerable.Range(0, Math.Clamp(zones, 0, 4)).Select(i => (output & (1UL << (40 + i))) != 0)];

    /// <summary><c>SetGamingLED</c>: which zones are lit.</summary>
    public static ulong ZoneEnableInput(IReadOnlyList<bool> zonesOn)
    {
        var input = 0x08UL;
        for (var i = 0; i < zonesOn.Count && i < 4; i++)
        {
            if (zonesOn[i])
                input |= 1UL << (40 + i);
        }
        return input;
    }

    /// <summary>
    /// Array form of <see cref="ZoneEnableInput"/> (<see cref="AcerSmbios.LedArrayLength"/>: 12 or 16 bytes): the
    /// integer's bytes and zeros, as PredatorSense sends it. NitroSense repeated bytes 0-3 in 8-11, which on the 12-byte
    /// form are the right and rear light bars' bytes.
    /// </summary>
    public static byte[] ZoneEnableArray(ulong input, int length)
    {
        var bytes = new byte[Math.Max(length, 8)];
        BitConverter.TryWriteBytes(bytes, input);
        return bytes;
    }

    // --- backlight auto-off (APGeAction, per-model hotkey function number) -----------------

    public static uint BacklightTimeoutQuery(byte hotkey) => 0x01u | ((uint)hotkey << 8) | 0x80000u;

    /// <summary>
    /// The brightness goes out as the nearest of <see cref="BrightnessLevels"/>: the firmware keeps four levels and off,
    /// and turns any other value into 50 % (AN515-57 V1.17).
    /// </summary>
    public static ulong BacklightTimeoutInput(byte hotkey, int brightnessPercent, int timeoutSeconds) =>
        0x02UL | ((ulong)hotkey << 8) | 0x80000UL | ((ulong)(byte)NearestLevel(brightnessPercent) << 32) | ((ulong)(byte)timeoutSeconds << 40);

    /// <summary>The level of <see cref="BrightnessLevels"/> closest to <paramref name="percent"/>.</summary>
    public static int NearestLevel(int percent) => BrightnessLevels.MinBy(level => Math.Abs(level - percent));

    public static int BacklightBrightnessValue(ulong output) => (int)((output >> 32) & 0xFF);

    public static int BacklightTimeoutValue(ulong output) => (int)((output >> 40) & 0xFF);

    /// <summary>The only auto-off delay NitroSense offers.</summary>
    public const int AutoOffSeconds = 30;

    // --- gaming profile settings (Windows key, LCD overdrive) ------------------------------

    public const uint ProfileQuery = 0;

    public static ulong WindowsKeyInput(bool enabled) => 0x02UL | ((enabled ? 1UL : 0UL) << 24);

    public static bool? WindowsKeyValue(ulong output) => ((output >> 24) & 0xFF) switch { 0 => false, 1 => true, _ => null };

    public static ulong LcdOverdriveInput(bool on) => 0x10UL | ((on ? 1UL : 0UL) << 48);

    /// <summary>1 = on; anything else = off.</summary>
    public static bool LcdOverdriveValue(ulong output) => ((output >> 48) & 0xFF) == 1;

    /// <summary>
    /// Overdrive depends on the panel, not the model: the firmware answers 0 or 1 for the panels that have it and 0xFF
    /// for the others (AN515-57 V1.17, which knows its panels by their EDID).
    /// </summary>
    public static bool LcdOverdriveSupported(ulong output) => ((output >> 48) & 0xFF) is 0 or 1;
}
