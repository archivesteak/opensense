using Windows.UI;

namespace OpenSense.App.Controls;

/// <summary>Colours the lighting previews share.</summary>
internal static class LightingColors
{
    /// <summary>The rainbow the firmware's Neon and Wave run through, at <paramref name="t"/> (0..1, wrapping).</summary>
    public static Color Hue(double t)
    {
        var h = (t % 1 + 1) % 1 * 6;
        var x = (byte)(255 * (1 - Math.Abs(h % 2 - 1)));
        return (int)h switch
        {
            0 => Color.FromArgb(255, 255, x, 0),
            1 => Color.FromArgb(255, x, 255, 0),
            2 => Color.FromArgb(255, 0, 255, x),
            3 => Color.FromArgb(255, 0, x, 255),
            4 => Color.FromArgb(255, x, 0, 255),
            _ => Color.FromArgb(255, 255, 0, x),
        };
    }
}
