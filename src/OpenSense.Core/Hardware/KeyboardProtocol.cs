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
}

public enum KeyboardDirection : byte
{
    Left = 1,
    Right = 2,
    Up = 3,
    Down = 4,
}

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor White { get; } = new(255, 255, 255);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public static RgbColor FromHex(string hex)
    {
        var s = hex.TrimStart('#');
        return s.Length == 6
            ? new RgbColor(Convert.ToByte(s[..2], 16), Convert.ToByte(s[2..4], 16), Convert.ToByte(s[4..], 16))
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

    public static bool UsesColor(KeyboardEffect effect) => effect is not (KeyboardEffect.Static or KeyboardEffect.Neon);

    public static bool UsesDirection(KeyboardEffect effect) => effect is KeyboardEffect.Wave or KeyboardEffect.Shifting;

    public static bool UsesSpeed(KeyboardEffect effect) => effect != KeyboardEffect.Static;

    /// <summary>
    /// The 16-byte <c>SetGamingKBBacklight</c> payload: an 8-byte record, repeated, with byte 9 set to 1
    /// (identical to what Acer's service sends).
    /// </summary>
    public static byte[] BacklightPayload(KeyboardEffect effect, int speed, int brightness, KeyboardDirection direction, RgbColor color)
    {
        Span<byte> record =
        [
            (byte)effect,
            (byte)(UsesSpeed(effect) ? Math.Clamp(speed, MinSpeed, MaxSpeed) : 0),
            (byte)Math.Clamp(brightness, 0, 100),
            (byte)(effect == KeyboardEffect.Wave ? 0x08 : 0),
            (byte)(UsesDirection(effect) ? direction : 0),
            UsesColor(effect) ? color.R : (byte)0,
            UsesColor(effect) ? color.G : (byte)0,
            UsesColor(effect) ? color.B : (byte)0,
        ];
        var payload = new byte[16];
        record.CopyTo(payload);
        record.CopyTo(payload.AsSpan(8));
        payload[9] = 1;
        return payload;
    }

    /// <summary><c>SetGamingRgbKb</c>: zone 1..4 and its colour.</summary>
    public static ulong ZoneColorInput(int zone, RgbColor color) =>
        (1UL << (zone - 1)) | ((ulong)color.R << 8) | ((ulong)color.G << 16) | ((ulong)color.B << 24);

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

    /// <summary>12-byte form of <see cref="ZoneEnableInput"/> for firmware with gaming interface ≥ 2.86.</summary>
    public static byte[] ZoneEnableArray(ulong input)
    {
        var bytes = new byte[12];
        BitConverter.TryWriteBytes(bytes, input);
        bytes.AsSpan(0, 4).CopyTo(bytes.AsSpan(8));
        return bytes;
    }

    // --- backlight auto-off (APGeAction, per-model hotkey function number) -----------------

    public static uint BacklightTimeoutQuery(byte hotkey) => 0x01u | ((uint)hotkey << 8) | 0x80000u;

    public static ulong BacklightTimeoutInput(byte hotkey, int brightnessPercent, int timeoutSeconds) =>
        0x02UL | ((ulong)hotkey << 8) | 0x80000UL | ((ulong)(byte)brightnessPercent << 32) | ((ulong)(byte)timeoutSeconds << 40);

    public static int BacklightBrightnessValue(ulong output) => (int)((output >> 32) & 0xFF);

    public static int BacklightTimeoutValue(ulong output) => (int)((output >> 40) & 0xFF);

    /// <summary>The only auto-off delay NitroSense offers.</summary>
    public const int AutoOffSeconds = 30;

    // --- gaming profile settings (Windows key, LCD overdrive) ------------------------------

    public const uint ProfileQuery = 0;

    public static ulong WindowsKeyInput(bool enabled) => 0x02UL | ((enabled ? 1UL : 0UL) << 24);

    public static bool? WindowsKeyValue(ulong output) => ((output >> 24) & 0xFF) switch { 0 => false, 1 => true, _ => null };

    public static ulong LcdOverdriveInput(bool on) => 0x10UL | ((on ? 1UL : 0UL) << 48);

    /// <summary>1 = on; anything else (including 0xFF, "never set") = off.</summary>
    public static bool LcdOverdriveValue(ulong output) => ((output >> 48) & 0xFF) == 1;
}
