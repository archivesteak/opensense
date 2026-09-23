using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml;
using OpenSense.App.Helpers;
using OpenSense.App.ViewModels;
using OpenSense.Core.Hardware;
using Windows.Foundation;
using Windows.UI;

namespace OpenSense.App.Controls;

/// <summary>A stylised laptop keyboard showing the four lighting zones as they will look.</summary>
public sealed partial class KeyboardPreview : CanvasElement
{
    // Rows of key widths in key units: main block (15 units) then numpad (4 units).
    private static readonly double[][] MainRows =
    [
        [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2],
        [1.5, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5],
        [1.75, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.25],
        [2.25, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.75],
        [1.25, 1, 1.25, 1.25, 5.25, 1.25, 1, 1, 1, 1],
    ];

    private static readonly double[][] PadRows =
    [
        [1, 1, 1, 1],
        [1, 1, 1, 1],
        [1, 1, 1, 1],
        [1, 1, 1, 1],
        [1, 1, 1, 1],
        [2, 1, 1],
    ];

    private const double MainUnits = 15, PadUnits = 4, Gap = 0.35;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(LightingViewModel), typeof(KeyboardPreview), new PropertyMetadata(null, (d, _) => ((KeyboardPreview)d).Invalidate()));

    public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
        nameof(Revision), typeof(int), typeof(KeyboardPreview), new PropertyMetadata(0, (d, _) => ((KeyboardPreview)d).Invalidate()));

    public LightingViewModel? Source
    {
        get => (LightingViewModel?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public int Revision
    {
        get => (int)GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    protected override void OnDraw(CanvasDrawingSession session, Size size)
    {
        var totalUnits = MainUnits + Gap * 2 + PadUnits;
        var unit = Math.Min(size.Width / totalUnits, size.Height / (MainRows.Length + 0.2));
        var width = unit * totalUnits;
        var left = (size.Width - width) / 2;
        var top = (size.Height - unit * MainRows.Length) / 2;

        using var keys = new CanvasCommandList(session);
        using (var ds = keys.CreateDrawingSession())
        {
            DrawBlock(ds, MainRows, left, top, unit, 0);
            DrawBlock(ds, PadRows, left + (MainUnits + Gap * 2) * unit, top, unit, MainUnits + Gap * 2);
        }

        // Glow under the keys, then the keys themselves.
        using var glow = new GaussianBlurEffect { Source = keys, BlurAmount = (float)(unit * 0.55), BorderMode = EffectBorderMode.Soft };
        using var faded = new OpacityEffect { Source = glow, Opacity = IsLight ? 0.35f : 0.8f };
        session.DrawImage(faded);
        session.DrawImage(keys);
    }

    private void DrawBlock(CanvasDrawingSession ds, double[][] rows, double left, double top, double unit, double unitOffset)
    {
        for (var r = 0; r < rows.Length; r++)
        {
            var x = 0.0;
            var height = r == 0 ? unit * 0.72 : unit * 0.86;
            var y = top + r * unit + (r == 0 ? unit * 0.14 : 0);
            foreach (var w in rows[r])
            {
                var keyLeft = left + x * unit;
                var keyWidth = w * unit - unit * 0.14;
                var centerUnits = unitOffset + x + w / 2;
                var color = KeyColor(centerUnits);
                ds.FillRoundedRectangle((float)keyLeft, (float)y, (float)keyWidth, (float)height, (float)(unit * 0.12), (float)(unit * 0.12), color);
                x += w;
            }
        }
    }

    /// <summary>Colour of a key whose centre is at <paramref name="units"/> from the keyboard's left edge.</summary>
    private Color KeyColor(double units)
    {
        var off = IsLight ? Color.FromArgb(0x26, 0, 0, 0) : Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
        if (Source is not { } vm)
            return off;

        var brightness = LightingViewModel.BrightnessNames.Count > 1 ? vm.BrightnessIndex / 4.0 : 1;
        if (brightness <= 0)
            return off;

        Color color;
        if (vm.IsStatic)
        {
            // Acer's 4-zone layout: the main block split into three zones, the numpad is the fourth.
            var zoneCount = Math.Max(1, vm.Zones.Count);
            int zone;
            if (zoneCount >= 2 && units >= MainUnits + Gap)
            {
                zone = zoneCount - 1;
            }
            else
            {
                var mainZones = zoneCount >= 2 ? zoneCount - 1 : 1;
                zone = (int)Math.Clamp(units / (MainUnits / mainZones), 0, mainZones - 1);
            }
            if (zone >= vm.Zones.Count || !vm.Zones[zone].On)
                return off;
            color = vm.Zones[zone].Color;
        }
        else if (vm.Effect == KeyboardEffect.Neon)
        {
            color = Hue(units / (MainUnits + Gap * 2 + PadUnits));
        }
        else
        {
            color = vm.EffectColor;
        }

        return TemperatureScale.Lerp(off, color, 0.35 + 0.65 * brightness);
    }

    private static Color Hue(double t)
    {
        var h = (t % 1) * 6;
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
