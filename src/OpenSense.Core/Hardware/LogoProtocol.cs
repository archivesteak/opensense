namespace OpenSense.Core.Hardware;

/// <summary>The lid logo's behaviours as <c>SetGamingLEDBehavior</c> numbers them (0 and 2 are never sent by Acer).</summary>
public enum LogoBehavior : byte
{
    Static = 1,
    Breathing = 3,
    Neon = 4,
}

/// <summary>
/// The lid logo on the embedded controller, as PredatorSense drives it: <c>SetGamingLEDColor</c> carries the colour,
/// brightness and speed, then <c>SetGamingLEDBehavior</c> the behaviour. Byte 0 of both is the LED group, 1 for the
/// logo. Nothing reads the logo back.
/// </summary>
public static class LogoProtocol
{
    /// <summary>The logo's LED group, also the input that asks whether there is one.</summary>
    public const uint Group = 0x01;

    public const int MinSpeed = 1;
    public const int MaxSpeed = 5;

    /// <summary>The logo's effects besides static, in Acer's order.</summary>
    public static IReadOnlyList<LogoBehavior> Effects { get; } = [LogoBehavior.Breathing, LogoBehavior.Neon];

    /// <summary>Neon runs through its own colours.</summary>
    public static bool UsesColor(LogoBehavior behavior) => behavior != LogoBehavior.Neon;

    /// <summary>Group, R, G, B, brightness 0-100, speed 1-5 (static carries a speed too, as Acer's app sends it).</summary>
    public static ulong ColorInput(RgbColor color, int brightness, int speed) =>
        Group | ((ulong)color.R << 8) | ((ulong)color.G << 16) | ((ulong)color.B << 24)
        | ((ulong)(byte)Math.Clamp(brightness, 0, 100) << 32) | ((ulong)(byte)Math.Clamp(speed, MinSpeed, MaxSpeed) << 40);

    public static ulong BehaviorInput(LogoBehavior behavior) => Group | ((ulong)behavior << 16);
}
