using System.Buffers.Binary;

namespace OpenSense.Core.Hardware.Hid;

/// <summary>A light on the embedded controller's lighting HID interface, by its id.</summary>
public enum Kyd100Light : byte
{
    /// <summary>All of the light bar at once.</summary>
    LightBar = 0x20,
    Keyboard = 0x21,
    FrontLightBar = 0x22,
    LeftLightBar = 0x23,
    RightLightBar = 0x24,
    RearLightBar = 0x25,
    TurboKey = 0x64,
    ModeKey = 0x65,
    Logo = 0x80,
    CoverLogo = 0x83,
    BaseLogo = 0x84,
}

/// <summary>The interface's modes (usage 0x41 of the update report).</summary>
public enum Kyd100Mode : byte
{
    Off = 1,
    Static = 2,
    Breathing = 4,
    Neon = 5,

    /// <summary>The firmware colours the light by the operating mode.</summary>
    FollowOperatingMode = 6,
    Wave = 7,
    Shifting = 8,
    Zoom = 9,
    Meteor = 10,
    Twinkling = 11,
    Combo = 12,
    Ripple = 13,
    Snake = 14,
    Disco = 15,
}

/// <summary>What the interface reports about one light: its number of LEDs and which modes it has.</summary>
public sealed record Kyd100LightInfo(Kyd100Light Id, int Leds, uint Attributes);

/// <summary>One update of a light (the values of report 0xA4).</summary>
/// <param name="Zones">Bit n addresses LED n; 0 addresses the whole light.</param>
public readonly record struct Kyd100Update(Kyd100Mode Mode, int Brightness, int Speed, int Direction, RgbColor Color, ushort Zones);

/// <summary>
/// The lighting HID interface of 2024+ Predators (usage page FF5A, any vendor): a list of lights, each with an info
/// report and an update report. The reports' values are packed one after another, each as wide as the report
/// descriptor declares it (bits / 8); a value the descriptor lacks is left out.
/// </summary>
public static class Kyd100Protocol
{
    public const ushort UsagePage = 0xFF5A;

    public const byte ListReport = 0xA1;
    public const byte SelectReport = 0xA2;
    public const byte InfoReport = 0xA3;
    public const byte UpdateReport = 0xA4;

    public const ushort LedsUsage = 0x35;
    public const ushort AttributesUsage = 0x31;

    /// <summary>The info report's values after the light id, in order.</summary>
    public static IReadOnlyList<ushort> InfoUsages { get; } = [0x34, 0x32, LedsUsage, 0x33, AttributesUsage];

    /// <summary>The update report's values after the light id: mode, brightness, speed, direction, R, G, B, zones.</summary>
    public static IReadOnlyList<ushort> UpdateUsages { get; } = [0x41, 0x43, 0x42, 0x44, 0x45, 0x46, 0x47, 0x48];

    /// <summary>Static colours go LED by LED, at most this many.</summary>
    public const int MaxZones = 16;

    public const int MinSpeed = 1;
    public const int KeyboardMaxSpeed = 9;
    public const int OtherMaxSpeed = 5;

    public const byte DirectionRight = 1;
    public const byte DirectionLeft = 2;
    public const byte DirectionUp = 3;
    public const byte DirectionDown = 4;

    /// <summary>The attribute bit that says a light has a mode.</summary>
    public static uint AttributeBit(Kyd100Mode mode) => mode switch
    {
        Kyd100Mode.Breathing => 0x0008,
        Kyd100Mode.Neon => 0x0010,
        Kyd100Mode.FollowOperatingMode => 0x0020,
        Kyd100Mode.Wave => 0x0040,
        Kyd100Mode.Shifting => 0x0080,
        Kyd100Mode.Zoom => 0x0100,
        Kyd100Mode.Meteor => 0x0200,
        Kyd100Mode.Twinkling => 0x0400,
        Kyd100Mode.Combo => 0x0800,
        Kyd100Mode.Ripple => 0x1000,
        Kyd100Mode.Snake => 0x2000,
        Kyd100Mode.Disco => 0x4000,
        _ => 0,
    };

    public static bool Matches(HidDeviceInfo device) => device.UsagePage == UsagePage && device.FeatureLength > 1;

    public static bool IsKeyboard(Kyd100Light light) => light == Kyd100Light.Keyboard;

    public static bool IsLightBar(Kyd100Light light) => light is Kyd100Light.LightBar or Kyd100Light.FrontLightBar
        or Kyd100Light.LeftLightBar or Kyd100Light.RightLightBar or Kyd100Light.RearLightBar;

    /// <summary>
    /// The modes besides static colours a light offers, in Acer's order: the keyboard any the interface reports, the
    /// light bars breathing, neon, operating mode, wave and twinkling, logos and keys breathing, neon and operating mode.
    /// </summary>
    public static IReadOnlyList<Kyd100Mode> Modes(Kyd100Light light, uint attributes)
    {
        Kyd100Mode[] candidates = IsKeyboard(light)
            ? [Kyd100Mode.Breathing, Kyd100Mode.Neon, Kyd100Mode.Wave, Kyd100Mode.Shifting, Kyd100Mode.Zoom, Kyd100Mode.Meteor,
               Kyd100Mode.Twinkling, Kyd100Mode.Ripple, Kyd100Mode.Snake, Kyd100Mode.Combo, Kyd100Mode.Disco, Kyd100Mode.FollowOperatingMode]
            : IsLightBar(light)
                ? [Kyd100Mode.Breathing, Kyd100Mode.Neon, Kyd100Mode.Wave, Kyd100Mode.Twinkling, Kyd100Mode.FollowOperatingMode]
                : [Kyd100Mode.Breathing, Kyd100Mode.Neon, Kyd100Mode.FollowOperatingMode];
        return [.. candidates.Where(m => (attributes & AttributeBit(m)) != 0)];
    }

    public static bool UsesColor(Kyd100Mode mode) => mode is Kyd100Mode.Static or Kyd100Mode.Breathing or Kyd100Mode.Shifting
        or Kyd100Mode.Zoom or Kyd100Mode.Meteor or Kyd100Mode.Twinkling;

    public static bool UsesDirection(Kyd100Mode mode) => mode is Kyd100Mode.Wave or Kyd100Mode.Shifting;

    public static int MaxSpeed(Kyd100Light light) => IsKeyboard(light) ? KeyboardMaxSpeed : OtherMaxSpeed;

    /// <summary>How many bytes a value takes in a report; 0 when the descriptor doesn't declare it.</summary>
    public static int Width(HidDeviceInfo device, byte report, ushort usage) =>
        device.FeatureValue(report, UsagePage, usage) is { } field ? field.BitSize / 8 : 0;

    /// <summary>A feature report buffer for <paramref name="report"/>, as long as the interface's.</summary>
    public static byte[] Buffer(HidDeviceInfo device, byte report)
    {
        var buffer = new byte[Math.Max(device.FeatureLength, 2)];
        buffer[0] = report;
        return buffer;
    }

    /// <summary>The ids of report 0xA1 (count, then one byte each); ids OpenSense doesn't know are left out.</summary>
    public static IReadOnlyList<Kyd100Light> DecodeList(ReadOnlySpan<byte> report)
    {
        if (report.Length < 2 || report[0] != ListReport)
            return [];
        var count = Math.Min(report[1], report.Length - 2);
        List<Kyd100Light> lights = [];
        foreach (var id in report.Slice(2, count))
        {
            if (Enum.IsDefined((Kyd100Light)id) && !lights.Contains((Kyd100Light)id))
                lights.Add((Kyd100Light)id);
        }
        return lights;
    }

    /// <summary>The ids of report 0xA1 as sent, for diagnostics.</summary>
    public static IReadOnlyList<byte> RawList(ReadOnlySpan<byte> report) =>
        report.Length < 2 || report[0] != ListReport ? [] : report.Slice(2, Math.Min(report[1], report.Length - 2)).ToArray();

    public static byte[] Select(HidDeviceInfo device, byte id)
    {
        var report = Buffer(device, SelectReport);
        report[1] = id;
        return report;
    }

    /// <summary>Report 0xA3 of light <paramref name="id"/>; null when it answers for another light.</summary>
    public static Kyd100LightInfo? DecodeInfo(HidDeviceInfo device, ReadOnlySpan<byte> report, Kyd100Light id)
    {
        if (report.Length < 2 || report[0] != InfoReport || report[1] != (byte)id)
            return null;
        var offset = 2;
        long leds = 0, attributes = 0;
        foreach (var usage in InfoUsages)
        {
            var width = Width(device, InfoReport, usage);
            if (width == 0)
                continue;
            if (offset + width > report.Length)
                return null;
            var value = Read(report.Slice(offset, width));
            if (usage == LedsUsage)
                leds = value;
            else if (usage == AttributesUsage)
                attributes = value;
            offset += width;
        }
        return new Kyd100LightInfo(id, (int)Math.Min(leds, int.MaxValue), (uint)attributes);
    }

    /// <summary>Report 0xA4 for light <paramref name="id"/>.</summary>
    public static byte[] Update(HidDeviceInfo device, Kyd100Light id, Kyd100Update update)
    {
        var report = Buffer(device, UpdateReport);
        report[1] = (byte)id;
        long[] values =
        [
            (byte)update.Mode, Math.Clamp(update.Brightness, 0, 100), update.Speed, update.Direction,
            update.Color.R, update.Color.G, update.Color.B, update.Zones,
        ];
        var offset = 2;
        for (var i = 0; i < UpdateUsages.Count; i++)
        {
            var width = Width(device, UpdateReport, UpdateUsages[i]);
            if (width == 0 || offset + width > report.Length)
                continue;
            Write(report.AsSpan(offset, width), values[i]);
            offset += width;
        }
        return report;
    }

    /// <summary>A little-endian value of up to eight bytes.</summary>
    private static long Read(ReadOnlySpan<byte> bytes)
    {
        Span<byte> value = stackalloc byte[8];
        value.Clear();
        bytes[..Math.Min(bytes.Length, 8)].CopyTo(value);
        return BinaryPrimitives.ReadInt64LittleEndian(value);
    }

    private static void Write(Span<byte> bytes, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        buffer[..Math.Min(bytes.Length, 8)].CopyTo(bytes);
    }
}
