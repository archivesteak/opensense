using System.Globalization;
using OpenSense.App.Localization;
using Windows.UI;

namespace OpenSense.App.Helpers;

public static class Units
{
    public static double ToDisplay(double celsius, bool fahrenheit) => fahrenheit ? celsius * 9 / 5 + 32 : celsius;

    public static string Temperature(double? celsius, bool fahrenheit) =>
        celsius is { } c ? string.Create(CultureInfo.CurrentCulture, $"{ToDisplay(c, fahrenheit):0}°") : "--";

    public static string TemperatureWithUnit(double celsius, bool fahrenheit) =>
        string.Create(CultureInfo.CurrentCulture, $"{ToDisplay(celsius, fahrenheit):0} °{(fahrenheit ? 'F' : 'C')}");

    /// <summary>A percentage (0–100), rounded, as the user's format writes it: "45%", "45 %", "%45".</summary>
    public static string Percent(double? value) =>
        value is { } v ? (v / 100).ToString("P0", CultureInfo.CurrentCulture) : "--";

    public static string Rpm(int? rpm) => Strings.Format("Unit_RpmValue", RpmNumber(rpm));

    public static string RpmNumber(int? rpm) =>
        rpm is { } r ? r.ToString("N0", CultureInfo.CurrentCulture) : "--";

    /// <summary>The unit after a fan speed number, with the space before it.</summary>
    public static string RpmSuffix { get; } = " " + Strings.Get("Unit_Rpm");
}

/// <summary>Maps a temperature to the shared cool → warm → hot → critical colour scale.</summary>
public static class TemperatureScale
{
    private static readonly (double Celsius, Color Color)[] Stops =
    [
        (35, Color.FromArgb(255, 0x3D, 0xB8, 0xF5)),
        (55, Color.FromArgb(255, 0x3D, 0xD6, 0x8C)),
        (75, Color.FromArgb(255, 0xFF, 0xB2, 0x24)),
        (92, Color.FromArgb(255, 0xFF, 0x4D, 0x4D)),
    ];

    public static Color ColorFor(double celsius)
    {
        if (celsius <= Stops[0].Celsius)
            return Stops[0].Color;
        for (var i = 1; i < Stops.Length; i++)
        {
            if (celsius <= Stops[i].Celsius)
            {
                var (t0, c0) = Stops[i - 1];
                var (t1, c1) = Stops[i];
                return Lerp(c0, c1, (celsius - t0) / (t1 - t0));
            }
        }
        return Stops[^1].Color;
    }

    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    public static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);
}

/// <summary>Fixed-size history of samples for the graphs (oldest first when read).</summary>
public sealed class HistoryBuffer(int capacity)
{
    private readonly double?[] _values = new double?[capacity];
    private int _next;
    private int _count;

    public int Capacity => capacity;

    public int Count => _count;

    public void Add(double? value)
    {
        _values[_next] = value;
        _next = (_next + 1) % capacity;
        _count = Math.Min(_count + 1, capacity);
    }

    public double? this[int index] => _values[(_next - _count + index + capacity) % capacity];

    public (double Min, double Max)? Range()
    {
        double? min = null, max = null;
        for (var i = 0; i < _count; i++)
        {
            if (this[i] is { } v)
            {
                min = Math.Min(min ?? v, v);
                max = Math.Max(max ?? v, v);
            }
        }
        return min is { } lo && max is { } hi ? (lo, hi) : null;
    }
}
