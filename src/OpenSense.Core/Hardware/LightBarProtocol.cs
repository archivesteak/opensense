namespace OpenSense.Core.Hardware;

/// <summary>The embedded controller's light bars, by the id their colour and on/off commands carry.</summary>
public enum LightBarId : byte
{
    Front = 1,
    Left = 2,
    Right = 4,
    Rear = 8,
}

/// <summary>A light bar the firmware reports, with its number of zones.</summary>
public sealed record LightBar(LightBarId Id, int Zones);

/// <summary>Effects of the light bars as <c>SetGamingKBBacklight</c> numbers them.</summary>
public enum LightBarEffect : byte
{
    Static = 0,
    Breathing = 1,
    Neon = 2,
    Wave = 3,
    Twinkling = 7,

    /// <summary>The rainbow snake (Acer's software sends it for "Snake").</summary>
    Snake = 9,
    Lightning = 10,
    Stack = 11,
    MotionPoint = 12,
    ZoomIn = 13,
}

/// <summary>
/// Light bars on the embedded controller (SMBIOS record 0x17 = 1), as PredatorSense drives them. Layout from
/// <c>GetGamingLED(0x10)</c>; each bar's zones switch on and off with <c>SetGamingLED</c> and take their colours from
/// <c>SetGamingRgbKb</c>, one bar at a time; effects and brightness go to every bar at once through
/// <c>SetGamingKBBacklight</c> (device byte 2), which has no bar id.
/// </summary>
public static class LightBarProtocol
{
    public const uint LayoutQuery = 0x10;

    /// <summary>Group byte of the on/off command (the keyboard's is 0x08).</summary>
    public const byte OnOffGroup = 0x10;

    /// <summary>Byte 9 of <c>SetGamingKBBacklight</c>: 1 keyboard, 2 light bars, 3 both.</summary>
    public const byte BacklightDevice = 0x02;

    public const int MinSpeed = 1;
    public const int MaxSpeed = 5;

    /// <summary>A rear bar with this many zones is the Infinity Mirror, which has its own effects.</summary>
    public const int InfinityMirrorZones = 5;

    /// <summary>
    /// Before gaming interface 2.86 the on/off command is this fixed value whatever the bar (front zones 1-2 on, the
    /// others left alone): Acer's software sends nothing else there.
    /// </summary>
    public const ulong LegacyOnOff = 0xFFF5_0000_0000_0010;

    /// <summary>The effects of an ordinary bar, in Acer's order.</summary>
    public static IReadOnlyList<LightBarEffect> BarEffects { get; } =
        [LightBarEffect.Breathing, LightBarEffect.Wave, LightBarEffect.Neon, LightBarEffect.Twinkling];

    /// <summary>The Infinity Mirror's effects, in Acer's order.</summary>
    public static IReadOnlyList<LightBarEffect> InfinityMirrorEffects { get; } =
    [
        LightBarEffect.Breathing, LightBarEffect.Wave, LightBarEffect.Neon, LightBarEffect.Snake, LightBarEffect.Lightning,
        LightBarEffect.Stack, LightBarEffect.MotionPoint, LightBarEffect.ZoomIn,
    ];

    /// <summary>Breathing and Twinkling take a colour; the others choose their own.</summary>
    public static bool UsesColor(LightBarEffect effect) => effect is LightBarEffect.Breathing or LightBarEffect.Twinkling;

    /// <summary>Only Wave has a direction, left or right.</summary>
    public static bool UsesDirection(LightBarEffect effect) => effect == LightBarEffect.Wave;

    /// <summary>
    /// The bars in a <c>GetGamingLED(0x10)</c> answer: its byte array (without the status) is the on/off command's
    /// layout from byte 1, so bar bytes sit at 5-8 (one byte each) or 5-12 (two each, <paramref name="wide"/>, gaming
    /// interface 2.91 on). Each byte holds four 2-bit zone fields, <c>11</c> for "no zone".
    /// </summary>
    public static IReadOnlyList<LightBar> DecodeLayout(ReadOnlySpan<byte> output, bool wide)
    {
        var bars = new List<LightBar>();
        var width = wide ? 2 : 1;
        LightBarId[] ids = [LightBarId.Front, LightBarId.Left, LightBarId.Right, LightBarId.Rear];
        for (var i = 0; i < ids.Length; i++)
        {
            var offset = 5 + i * width;
            if (offset + width > output.Length)
                break;
            var fields = output.Slice(offset, width);
            // A bar reading all zeros is absent (as Acer's 11-byte check has it); otherwise count its zones.
            if (!fields.ContainsAnyExcept((byte)0))
                continue;
            var zones = 0;
            foreach (var b in fields)
            {
                for (var shift = 0; shift < 8; shift += 2)
                {
                    if (((b >> shift) & 0b11) != 0b11)
                        zones++;
                }
            }
            if (zones > 0)
                bars.Add(new LightBar(ids[i], zones));
        }
        return bars;
    }

    /// <summary>
    /// <c>SetGamingLED</c> for one bar in the array form (12 bytes from gaming interface 2.86, 16 from 2.91): group
    /// 0x10, the bar's zone fields (<c>01</c> lit, <c>00</c> dark), every other bar 0xFF so it is left alone.
    /// </summary>
    public static byte[] OnOffArray(LightBarId bar, IReadOnlyList<bool> zonesOn, int length)
    {
        var wide = length >= 16;
        var width = wide ? 2 : 1;
        var bytes = new byte[wide ? 16 : 12];
        bytes[0] = OnOffGroup;
        LightBarId[] ids = [LightBarId.Front, LightBarId.Left, LightBarId.Right, LightBarId.Rear];
        for (var i = 0; i < ids.Length; i++)
        {
            var offset = 6 + i * width;
            if (ids[i] != bar)
            {
                bytes.AsSpan(offset, width).Fill(0xFF);
                continue;
            }
            var fields = 0;
            for (var zone = 0; zone < zonesOn.Count && zone < width * 4; zone++)
            {
                if (zonesOn[zone])
                    fields |= 1 << (zone * 2);
            }
            bytes[offset] = (byte)fields;
            if (wide)
                bytes[offset + 1] = (byte)(fields >> 8);
        }
        return bytes;
    }

    /// <summary><c>SetGamingRgbKb</c> for zone <paramref name="zone"/> (1-based) of a bar.</summary>
    public static ulong ZoneColorInput(LightBarId bar, int zone, RgbColor color) =>
        ((ulong)color.R << 8) | ((ulong)color.G << 16) | ((ulong)color.B << 24) | ((ulong)bar << 32) | (1UL << (39 + zone));

    /// <summary>
    /// <c>SetGamingKBBacklight</c> for every bar: effect, speed 1-5, brightness 0-100, direction, colour, then 03 02.
    /// Static sends only the brightness (the colours go per zone).
    /// </summary>
    public static byte[] BacklightPayload(LightBarEffect effect, int speed, int brightness, byte direction, RgbColor color)
    {
        var payload = new byte[16];
        payload[0] = (byte)effect;
        payload[2] = (byte)Math.Clamp(brightness, 0, 100);
        if (effect != LightBarEffect.Static)
        {
            payload[1] = (byte)Math.Clamp(speed, MinSpeed, MaxSpeed);
            payload[4] = UsesDirection(effect) ? direction : (byte)1;
            if (effect is not (LightBarEffect.Neon or LightBarEffect.Wave))
            {
                payload[5] = color.R;
                payload[6] = color.G;
                payload[7] = color.B;
            }
        }
        payload[8] = 0x03;
        payload[9] = BacklightDevice;
        return payload;
    }

    /// <summary>Wave's direction byte as PredatorSense numbers it: 1 right, 2 left (not verified on a light bar).</summary>
    public const byte DirectionRight = 1;

    public const byte DirectionLeft = 2;
}
