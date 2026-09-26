namespace OpenSense.Core.Hardware.Hid;

/// <summary>A Darfon light: the lid logo, the light bar next to it ("MilkyWay"), the InfiniteRing.</summary>
public enum DarfonPart
{
    CoverLogo,
    LightBar,
    InfiniteRing,
}

/// <summary>Effects as Darfon lights number them (the logo's differ from the light bar's and ring's).</summary>
public enum DarfonEffect : byte
{
    Static = 1,
    Breathing = 2,

    // The cover logo's.
    LogoWave = 3,
    LogoSnake = 5,
    LogoSwiping = 7,
    LogoNeon = 8,

    // The light bar's and the InfiniteRing's.
    Dazzling = 10,
    Snake = 87,
    Ripple = 88,
    RowWave = 93,
    Racing = 94,
    Swiping = 97,
    Disco = 99,
}

/// <summary>A Darfon USB device Acer's software drives, and which of its lights it has.</summary>
/// <param name="Model2025">A 2025 or later device (its cover logo swipes instead of neon).</param>
public sealed record DarfonModel(ushort ProductId, IReadOnlyList<DarfonPart> Parts, bool Model2025);

/// <summary>
/// Darfon lights (vendor 0D62, usage page FF01): the per-key keyboards' 8-byte checksummed commands with commands of
/// their own. Pure encoding; <see cref="DarfonDevice"/> sends them.
/// </summary>
public static class DarfonProtocol
{
    public const ushort VendorId = UsbKeyboardProtocol.DarfonVendor;
    public const ushort UsagePage = 0xFF01;

    public const int MinSpeed = 1;
    public const int MaxSpeed = 5;

    /// <summary>The cover logo's static colours are three LEDs.</summary>
    public const int LogoLeds = 3;

    /// <summary>Between the reports of an effect or the logo's colours.</summary>
    public static readonly TimeSpan CommandDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Between the reports of the light bar's and ring's static colour, and after opening.</summary>
    public static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(20);

    public static DarfonModel? Find(ushort vendorId, ushort productId) => vendorId != VendorId ? null : productId switch
    {
        0xBA51 => new(productId, [DarfonPart.CoverLogo], false),
        0xA00A or 0xA01A => new(productId, [DarfonPart.CoverLogo, DarfonPart.LightBar], true),
        0xA54A => new(productId, [DarfonPart.CoverLogo, DarfonPart.LightBar], true),
        0xA20A or 0xA21A or 0xA55A => new(productId, [DarfonPart.InfiniteRing], true),
        _ => null,
    };

    public static bool Matches(HidDeviceInfo device) =>
        Find(device.VendorId, device.ProductId) is not null && device.UsagePage == UsagePage && device.FeatureLength >= UsbKeyboardProtocol.CommandLength;

    /// <summary>The output report Acer's software sends when it opens the device (65 bytes: report id 0, then 41 01).</summary>
    public static byte[] Hello()
    {
        var report = new byte[65];
        report[1] = 0x41;
        report[2] = 0x01;
        return report;
    }

    /// <summary>Which light a command addresses on its device.</summary>
    public static byte SubId(DarfonPart part) => part == DarfonPart.LightBar ? (byte)5 : (byte)0;

    /// <summary>Speed 1..5 (higher is faster) goes out as 11 − 2 × speed.</summary>
    public static byte SpeedByte(int speed) => (byte)(11 - 2 * Math.Clamp(speed, MinSpeed, MaxSpeed));

    /// <summary>An effect: its colour, then the effect itself (m = 8 for random colours; the direction + 1).</summary>
    public static IReadOnlyList<byte[]> EffectSequence(DarfonPart part, DarfonEffect effect, int speed, int brightness, byte direction,
        RgbColor color, bool random)
    {
        var sub = SubId(part);
        return
        [
            UsbKeyboardProtocol.Command(0x14, (byte)effect, sub, color.R, color.G, color.B, 0x00),
            UsbKeyboardProtocol.Command(0x08, sub, (byte)effect, SpeedByte(speed), (byte)Math.Clamp(brightness, 0, 100),
                random ? (byte)0x08 : (byte)0x00, (byte)(direction + 1)),
        ];
    }

    public static IReadOnlyList<byte[]> OffSequence(DarfonPart part) =>
    [
        UsbKeyboardProtocol.Command(0x14, 0x01),
        UsbKeyboardProtocol.Command(0x08, SubId(part), 0x01, 0x01, 0x00, 0x01, 0x00),
    ];

    /// <summary>The cover logo's static colours: the brightness, then each of its three LEDs (1..3).</summary>
    public static IReadOnlyList<byte[]> LogoStaticSequence(int brightness, IReadOnlyList<RgbColor> leds)
    {
        List<byte[]> reports = [UsbKeyboardProtocol.Command(0x08, 0x01, 0x01, 0x05, (byte)Math.Clamp(brightness, 0, 100), 0x01, 0x00)];
        for (var led = 0; led < LogoLeds; led++)
        {
            var color = leds.Count == 0 ? default : leds[Math.Min(led, leds.Count - 1)];
            reports.Add(UsbKeyboardProtocol.Command(0x14, 0x01, (byte)(led + 1), color.R, color.G, color.B, 0x03));
        }
        return reports;
    }

    /// <summary>The light bar's and ring's static colour (one for the whole light), in three steps 20 ms apart.</summary>
    public static IReadOnlyList<IReadOnlyList<byte[]>> BarStaticSteps(DarfonPart part, int brightness, RgbColor color) =>
    [
        [UsbKeyboardProtocol.Command(0x08, 0x02), UsbKeyboardProtocol.Command(0x08, 0x00, 0x01, 0x05, (byte)Math.Clamp(brightness, 0, 100), 0x01, 0x00)],
        [UsbKeyboardProtocol.Command(0x3A)],
        [UsbKeyboardProtocol.Command(0x14, 0x00, SubId(part), color.R, color.G, color.B, 0x00)],
    ];

    /// <summary>The effects Acer's software offers besides static colours (the light bar's and ring's "Wave" is their row wave).</summary>
    public static IReadOnlyList<DarfonEffect> Effects(DarfonPart part, bool model2025) => part == DarfonPart.CoverLogo
        ? [DarfonEffect.Breathing, DarfonEffect.LogoWave, DarfonEffect.LogoSnake, model2025 ? DarfonEffect.LogoSwiping : DarfonEffect.LogoNeon]
        : [DarfonEffect.Breathing, DarfonEffect.RowWave, DarfonEffect.Snake, DarfonEffect.Ripple, DarfonEffect.Dazzling, DarfonEffect.Swiping,
           DarfonEffect.Racing, DarfonEffect.Disco];

    public static bool UsesColor(DarfonEffect effect) => effect is DarfonEffect.Static or DarfonEffect.Breathing
        or DarfonEffect.LogoSnake or DarfonEffect.Snake;

    public static bool TakesRandomColor(DarfonEffect effect) => effect == DarfonEffect.Breathing;

    public static bool UsesDirection(DarfonEffect effect) => effect == DarfonEffect.LogoWave;
}
