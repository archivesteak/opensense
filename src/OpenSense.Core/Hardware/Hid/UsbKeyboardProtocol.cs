using OpenSense.Core.Lighting;

namespace OpenSense.Core.Hardware.Hid;

public enum UsbKeyboardMaker
{
    Chicony,
    Sunrex,

    /// <summary>Darfon keyboards: backlight auto-off and the Windows key only (Acer's software drives no lights on them).</summary>
    Darfon,
}

/// <summary>Which of Acer's effect lists a per-key keyboard gets.</summary>
public enum UsbKeyboardGeneration
{
    /// <summary>No lights of its own to drive (Darfon keyboards).</summary>
    None,
    Chicony,

    /// <summary>Sunrex 766x.</summary>
    Sunrex2023,

    /// <summary>Sunrex 666x, 866x.</summary>
    Sunrex2024,

    /// <summary>Sunrex 667x, 867x, 767x, 668x, 868x.</summary>
    Sunrex2025,
}

/// <summary>Effects as the per-key keyboards number them.</summary>
public enum UsbKeyboardEffect : byte
{
    Static = 1,
    Breathing = 2,
    Wave = 3,
    Snake = 5,
    Ripple = 6,
    Neon = 8,
    Raindrop = 10,
    Lightning = 18,
    FollowOperatingMode = 34,
    Fireball = 39,
    Snow = 40,
    Heartbeat = 41,
    Dazzling = 42,
    Matrix = 43,
    Swiping = 44,
    Racing = 45,
    Sprouting = 46,
    Disco = 47,
    PingPong = 48,
    RowWave = 49,
    LightShow = 50,
}

/// <summary>The MagForce keys' effects, set through the Sunrex keyboard.</summary>
public enum MagKeyEffect : byte
{
    Off = 0x40,
    Static = 0x41,
    Breathing = 0x42,
    Wave = 0x43,
    Snake = 0x44,
    Neon = 0x45,
    Star = 0x47,
    Rainbow = 0x48,

    /// <summary>Goes out as 0x4C.</summary>
    Slash = 0x49,
    Blasting = 0x4A,

    /// <summary>The colours uploaded for the MagForce keys.</summary>
    PerKey = 0x4F,
    RowWave = 0x50,
    Swiping = 0x51,
}

/// <summary>A USB keyboard Acer's software knows, by vendor and product.</summary>
/// <param name="Layout">As Acer's keyboard-type table gives it; null for the one type it doesn't name.</param>
public sealed record UsbKeyboardModel(ushort VendorId, ushort ProductId, UsbKeyboardMaker Maker, UsbKeyboardGeneration Generation, KeyboardLayout? Layout)
{
    /// <summary>OpenSense drives its lights (Chicony and Sunrex keyboards).</summary>
    public bool Lighting => Generation != UsbKeyboardGeneration.None;

    /// <summary>The per-key upload carries this many LEDs (Chicony 512 bytes, Sunrex 504).</summary>
    public int Leds => Maker == UsbKeyboardMaker.Chicony ? 128 : 126;
}

/// <summary>
/// Per-key USB keyboards (docs/PROTOCOL.md "Per-key USB keyboards"): 8-byte feature reports with a checksum for
/// commands, 64-byte output reports for per-key colours. Pure encoding; <see cref="UsbKeyboardDevice"/> sends them.
/// </summary>
public static class UsbKeyboardProtocol
{
    public const ushort ChiconyVendor = 0x04F2;
    public const ushort SunrexVendor = 0x05AF;
    public const ushort DarfonVendor = 0x0D62;

    /// <summary>The lighting interface's usage page.</summary>
    public const ushort LightingUsagePage = 0xFF02;

    /// <summary>A command's feature report: report id 0 and eight bytes.</summary>
    public const int CommandLength = 9;

    public const int DataLength = 64;
    public const int DataReports = 8;
    public const int MagKeyDataLength = 48;

    public const int MaxBrightness = 50;
    public const int MinSpeed = 1;
    public const int MaxSpeed = 9;

    public const byte DirectionRight = 1;
    public const byte DirectionLeft = 2;
    public const byte DirectionUp = 3;
    public const byte DirectionDown = 4;

    /// <summary>Between the reports of a command sequence.</summary>
    public static readonly TimeSpan CommandDelay = TimeSpan.FromMilliseconds(15);

    /// <summary>Before and after the per-key data, and before reading an answer.</summary>
    public static readonly TimeSpan UploadDelay = TimeSpan.FromMilliseconds(20);

    /// <summary>Between the per-key data reports.</summary>
    public static readonly TimeSpan DataDelay = TimeSpan.FromMilliseconds(2);

    /// <summary>The MagForce keys, and the order of their colours in the upload (three LEDs each).</summary>
    public static IReadOnlyList<string> MagKeys { get; } = ["KeyW", "KeyA", "KeyS", "KeyD"];

    /// <summary>Acer's keyboard-type table (0 US, 1 UK, 2 Japanese; Darfon 0ABD's type 3 isn't named).</summary>
    public static UsbKeyboardModel? Find(ushort vendorId, ushort productId)
    {
        switch (vendorId)
        {
            case ChiconyVendor when productId is 0x0117 or 0x011A or 0x0119:
                return new(vendorId, productId, UsbKeyboardMaker.Chicony, UsbKeyboardGeneration.Chicony,
                    productId switch { 0x0117 => KeyboardLayout.Ansi, 0x011A => KeyboardLayout.Iso, _ => KeyboardLayout.Jis });
            case SunrexVendor when (productId & 0xF) is >= 0xA and <= 0xE:
                var family = productId >> 4;
                UsbKeyboardGeneration? generation = family switch
                {
                    0x766 => UsbKeyboardGeneration.Sunrex2023,
                    0x666 or 0x866 => UsbKeyboardGeneration.Sunrex2024,
                    0x667 or 0x867 or 0x767 or 0x668 or 0x868 => UsbKeyboardGeneration.Sunrex2025,
                    _ => null,
                };
                if (generation is null)
                    return null;
                // The 766 keyboards' letters stand for other layouts than the later families'.
                var layout = family == 0x766
                    ? (productId & 0xF) switch { 0xA or 0xE => KeyboardLayout.Iso, 0xB => KeyboardLayout.Jis, _ => KeyboardLayout.Ansi }
                    : (productId & 0xF) switch { 0xB or 0xE => KeyboardLayout.Iso, 0xC => KeyboardLayout.Jis, _ => KeyboardLayout.Ansi };
                return new(vendorId, productId, UsbKeyboardMaker.Sunrex, generation.Value, layout);
            case DarfonVendor when DarfonKeyboards.TryGetValue(productId, out var type):
                return new(vendorId, productId, UsbKeyboardMaker.Darfon, UsbKeyboardGeneration.None, type);
            default:
                return null;
        }
    }

    private static readonly Dictionary<ushort, KeyboardLayout?> DarfonKeyboards = new()
    {
        [0xFABC] = KeyboardLayout.Ansi, [0xFABE] = KeyboardLayout.Iso,
        [0x0ABA] = KeyboardLayout.Ansi, [0x0ABB] = KeyboardLayout.Iso, [0x0ABC] = KeyboardLayout.Jis, [0x0ABD] = null,
        [0x6A0C] = KeyboardLayout.Ansi, [0x6A0D] = KeyboardLayout.Iso, [0x6666] = KeyboardLayout.Ansi,
        [0x9A1A] = KeyboardLayout.Ansi, [0x9A1B] = KeyboardLayout.Iso,
        [0x7CB0] = KeyboardLayout.Ansi, [0x7CB1] = KeyboardLayout.Iso, [0x9CBB] = KeyboardLayout.Iso,
        [0x0B9D] = KeyboardLayout.Ansi, [0x0B9E] = KeyboardLayout.Iso, [0x0B9F] = KeyboardLayout.Ansi, [0x0BA0] = KeyboardLayout.Iso,
    };

    /// <summary>The interface that takes the keyboard's commands (Acer's software picks the one with 9-byte feature reports).</summary>
    public static bool IsCommandInterface(HidDeviceInfo device) =>
        Find(device.VendorId, device.ProductId) is not null && device.FeatureLength == CommandLength;

    /// <summary>The interface that takes the keyboard's lighting (usage page FF02).</summary>
    public static bool IsLightingInterface(HidDeviceInfo device) =>
        Find(device.VendorId, device.ProductId) is { Lighting: true } && device.UsagePage == LightingUsagePage
        && device.FeatureLength >= CommandLength;

    /// <summary>A command report: report id 0, the seven bytes, then 0xFF minus their sum.</summary>
    public static byte[] Command(params ReadOnlySpan<byte> bytes)
    {
        var report = new byte[CommandLength];
        bytes[..Math.Min(bytes.Length, 7)].CopyTo(report.AsSpan(1));
        var sum = 0;
        for (var i = 1; i < 8; i++)
            sum += report[i];
        report[8] = (byte)(0xFF - sum);
        return report;
    }

    public static byte[] Prepare() => Command(0xB1);

    public static byte[] Clear() => Command(0x08, 0x02);

    public static byte[] Color(byte slot, RgbColor color) => Command(0x14, 0x00, slot, color.R, color.G, color.B, 0x00);

    public static byte[] Effect(byte effect, int speed, int brightness, byte color, byte direction) =>
        Command(0x08, 0x02, effect, SpeedByte(speed), BrightnessByte(brightness), color, direction);

    public static byte[] SunrexOff() => Command(0x08, 0x01);

    public static byte[] UploadStart(bool magKey) => Command(magKey ? (byte)0x13 : (byte)0x12, 0x00, 0x00, 0x08);

    public static byte[] ShowUpload(int brightness, bool magKey) =>
        Command(0x08, 0x02, magKey ? (byte)MagKeyEffect.PerKey : (byte)0x33, 0x05, BrightnessByte(brightness), 0x08, 0x01);

    /// <summary>Backlight off after 30 s without typing.</summary>
    public static byte[] AutoOff(bool on) => Command(0x30, 0x01, on ? (byte)1 : (byte)0);

    public static byte[] ReadAutoOff() => Command(0xB0);

    /// <summary>The Windows and Menu keys: locked (3) or working (0).</summary>
    public static byte[] WindowsKeys(bool locked) => Command(0x03, locked ? (byte)3 : (byte)0);

    public static byte[] ReadWindowsKeys() => Command(0x83);

    public static byte[] ReadState() => Command(0x88);

    /// <summary>The answer to <see cref="ReadAutoOff"/> (report id first).</summary>
    public static bool DecodeAutoOff(ReadOnlySpan<byte> answer) => answer.Length > 3 && answer[3] != 0;

    public static bool DecodeWindowsKeysLocked(ReadOnlySpan<byte> answer) => answer.Length > 2 && (answer[2] & 1) != 0;

    /// <summary>The brightness in <see cref="ReadState"/>'s answer, 0..100.</summary>
    public static int DecodeBrightness(ReadOnlySpan<byte> answer) =>
        answer.Length > 5 ? Math.Clamp(answer[5] * 100 / MaxBrightness, 0, 100) : 0;

    /// <summary>0..100 as the keyboard's 0..50.</summary>
    public static byte BrightnessByte(int brightness) => (byte)Math.Clamp((brightness * MaxBrightness + 50) / 100, 0, MaxBrightness);

    /// <summary>Speed 1..9 (higher is faster) goes out as 10 − speed.</summary>
    public static byte SpeedByte(int speed) => (byte)(10 - Math.Clamp(speed, MinSpeed, MaxSpeed));

    /// <summary>
    /// An effect: on Chicony prepare, the colour (slot 0), the effect; on Sunrex prepare, clear, the effect, the colour
    /// (the 766 keyboards take it all twice). Sunrex takes random colours and a direction for Wave only.
    /// </summary>
    public static IReadOnlyList<byte[]> EffectSequence(UsbKeyboardModel model, UsbKeyboardEffect effect, int speed, int brightness,
        byte direction, RgbColor color, bool random)
    {
        if (model.Maker == UsbKeyboardMaker.Chicony)
        {
            if (effect == UsbKeyboardEffect.FollowOperatingMode)
                return [Color(0x01, color), Command(0x08, 0x03, (byte)UsbKeyboardEffect.FollowOperatingMode, 0x05, 0x32, 0x02, 0x01)];
            return [Prepare(), Color(0x00, color), Effect((byte)effect, speed, brightness, 0x01, (byte)(direction + 1))];
        }
        return SunrexSequence(model, (byte)effect, speed, brightness,
            effect == UsbKeyboardEffect.Wave ? (byte)(direction + 1) : (byte)0, color, 0x00, random);
    }

    /// <summary>Everything dark.</summary>
    public static IReadOnlyList<byte[]> OffSequence(UsbKeyboardModel model) => model.Maker == UsbKeyboardMaker.Chicony
        ? [Prepare(), Clear(), Color(0x00, default), Command(0x08, 0x02, 0x01, 0x01, 0x00, 0x01, 0x00)]
        : [SunrexOff()];

    /// <summary>A MagForce key effect: Sunrex's sequence, the colour in slot 1 (Slash, which has none, in slot 0).</summary>
    public static IReadOnlyList<byte[]> MagKeySequence(UsbKeyboardModel model, MagKeyEffect effect, int speed, int brightness,
        byte direction, RgbColor color, bool random)
    {
        var value = effect == MagKeyEffect.Slash ? (byte)0x4C : (byte)effect;
        var slot = (byte)effect is >= 0x40 and <= 0x4F && effect != MagKeyEffect.Slash ? (byte)0x01 : (byte)0x00;
        return SunrexSequence(model, value, speed, brightness, effect == MagKeyEffect.Wave ? (byte)(direction + 1) : (byte)0,
            color, slot, random);
    }

    private static byte[][] SunrexSequence(UsbKeyboardModel model, byte effect, int speed, int brightness, byte direction,
        RgbColor color, byte slot, bool random)
    {
        byte[][] once = [Prepare(), Clear(), Effect(effect, speed, brightness, random ? (byte)0xE0 : (byte)0x00, direction), Color(slot, color)];
        return model.Generation == UsbKeyboardGeneration.Sunrex2023 ? [.. once, .. once] : once;
    }

    /// <summary>
    /// The per-key colours as eight output reports (report id 0, 64 bytes each): LED n's colour at bytes 4n+1..4n+3.
    /// LEDs past what the keyboard's upload carries are left out.
    /// </summary>
    public static IReadOnlyList<byte[]> UploadData(UsbKeyboardModel model, IReadOnlyDictionary<int, RgbColor> leds)
    {
        var data = new byte[DataLength * DataReports];
        foreach (var (led, color) in leds)
        {
            if (led < 0 || led >= model.Leds)
                continue;
            data[4 * led + 1] = color.R;
            data[4 * led + 2] = color.G;
            data[4 * led + 3] = color.B;
        }
        return [.. Enumerable.Range(0, DataReports).Select(i =>
        {
            var report = new byte[DataLength + 1];
            data.AsSpan(i * DataLength, DataLength).CopyTo(report.AsSpan(1));
            return report;
        })];
    }

    /// <summary>The MagForce keys' colours (W, A, S, D, three LEDs each as 00 R G B) as one output report.</summary>
    public static byte[] MagKeyData(IReadOnlyList<RgbColor> keys)
    {
        var report = new byte[MagKeyDataLength + 1];
        for (var key = 0; key < Math.Min(keys.Count, MagKeys.Count); key++)
        {
            for (var led = 0; led < 3; led++)
            {
                var at = 1 + (key * 3 + led) * 4;
                report[at + 1] = keys[key].R;
                report[at + 2] = keys[key].G;
                report[at + 3] = keys[key].B;
            }
        }
        return report;
    }

    /// <summary>The effects Acer's software offers on a keyboard besides static colours, in its order.</summary>
    public static IReadOnlyList<UsbKeyboardEffect> Effects(UsbKeyboardGeneration generation) => generation switch
    {
        UsbKeyboardGeneration.Chicony or UsbKeyboardGeneration.Sunrex2023 =>
        [
            UsbKeyboardEffect.Breathing, UsbKeyboardEffect.Wave, UsbKeyboardEffect.Snake, UsbKeyboardEffect.Ripple, UsbKeyboardEffect.Neon,
            UsbKeyboardEffect.Raindrop, UsbKeyboardEffect.Lightning, UsbKeyboardEffect.Fireball, UsbKeyboardEffect.Snow,
            UsbKeyboardEffect.Heartbeat, UsbKeyboardEffect.FollowOperatingMode,
        ],
        UsbKeyboardGeneration.Sunrex2024 =>
        [
            UsbKeyboardEffect.Breathing, UsbKeyboardEffect.Wave, UsbKeyboardEffect.Snake, UsbKeyboardEffect.Ripple, UsbKeyboardEffect.Neon,
            UsbKeyboardEffect.Raindrop, UsbKeyboardEffect.Lightning, UsbKeyboardEffect.Fireball, UsbKeyboardEffect.Snow,
            UsbKeyboardEffect.Heartbeat, UsbKeyboardEffect.Dazzling, UsbKeyboardEffect.Matrix, UsbKeyboardEffect.FollowOperatingMode,
        ],
        UsbKeyboardGeneration.Sunrex2025 =>
        [
            UsbKeyboardEffect.Breathing, UsbKeyboardEffect.Wave, UsbKeyboardEffect.Snake, UsbKeyboardEffect.Ripple,
            UsbKeyboardEffect.Raindrop, UsbKeyboardEffect.Fireball, UsbKeyboardEffect.Dazzling, UsbKeyboardEffect.Swiping,
            UsbKeyboardEffect.Racing, UsbKeyboardEffect.Sprouting, UsbKeyboardEffect.Disco, UsbKeyboardEffect.PingPong,
            UsbKeyboardEffect.LightShow, UsbKeyboardEffect.FollowOperatingMode,
        ],
        _ => [],
    };

    public static bool UsesColor(UsbKeyboardEffect effect) => effect is UsbKeyboardEffect.Static or UsbKeyboardEffect.Breathing
        or UsbKeyboardEffect.Snake or UsbKeyboardEffect.Ripple or UsbKeyboardEffect.Raindrop or UsbKeyboardEffect.Lightning
        or UsbKeyboardEffect.Fireball or UsbKeyboardEffect.Snow or UsbKeyboardEffect.Heartbeat or UsbKeyboardEffect.Dazzling
        or UsbKeyboardEffect.Matrix;

    /// <summary>Sunrex keyboards can pick random colours for these.</summary>
    public static bool TakesRandomColor(UsbKeyboardEffect effect) => effect is UsbKeyboardEffect.Breathing or UsbKeyboardEffect.Snake
        or UsbKeyboardEffect.Ripple or UsbKeyboardEffect.Raindrop or UsbKeyboardEffect.Lightning or UsbKeyboardEffect.Fireball
        or UsbKeyboardEffect.Snow or UsbKeyboardEffect.Heartbeat or UsbKeyboardEffect.Matrix;

    /// <summary>The MagForce keys' effects besides static colours, in Acer's order: the 2024 models' or the 2025 models'.</summary>
    public static IReadOnlyList<MagKeyEffect> MagKeyEffects(bool model2025) => model2025
        ? [MagKeyEffect.Breathing, MagKeyEffect.Wave, MagKeyEffect.Snake, MagKeyEffect.Star, MagKeyEffect.Blasting, MagKeyEffect.Swiping,
           MagKeyEffect.Rainbow, MagKeyEffect.Slash]
        : [MagKeyEffect.Breathing, MagKeyEffect.Wave, MagKeyEffect.Snake, MagKeyEffect.Neon, MagKeyEffect.Star, MagKeyEffect.Blasting,
           MagKeyEffect.Rainbow, MagKeyEffect.Slash];

    public static bool UsesColor(MagKeyEffect effect) => effect is MagKeyEffect.Static or MagKeyEffect.Breathing or MagKeyEffect.Snake
        or MagKeyEffect.Star or MagKeyEffect.Blasting;

    public static bool TakesRandomColor(MagKeyEffect effect) => effect is MagKeyEffect.Breathing or MagKeyEffect.Snake
        or MagKeyEffect.Star or MagKeyEffect.Blasting;

    /// <summary>The Predator Helios models with MagForce keys: 2024's (PH16-72, PH18-72) and 2025's (PH16-73, PH18-73).</summary>
    public static bool? MagKeyModel2025(string? model) => model switch
    {
        null => null,
        _ when model.Contains("PH16-72", StringComparison.OrdinalIgnoreCase) || model.Contains("PH18-72", StringComparison.OrdinalIgnoreCase) => false,
        _ when model.Contains("PH16-73", StringComparison.OrdinalIgnoreCase) || model.Contains("PH18-73", StringComparison.OrdinalIgnoreCase) => true,
        _ => null,
    };
}
