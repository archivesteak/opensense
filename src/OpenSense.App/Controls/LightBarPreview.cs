using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml;
using OpenSense.App.Helpers;
using OpenSense.App.ViewModels;
using OpenSense.Core.Lighting;
using Windows.Foundation;
using Windows.UI;

namespace OpenSense.App.Controls;

/// <summary>A light bar seen from the front, its zones side by side, as it will look.</summary>
public sealed partial class LightBarPreview : CanvasElement
{
    /// <summary>Segments drawn per zone, so effects that move along the bar show their gradient.</summary>
    private const int SegmentsPerZone = 6;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(LightingViewModel), typeof(LightBarPreview), new PropertyMetadata(null, (d, _) => ((LightBarPreview)d).Invalidate()));

    public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
        nameof(Revision), typeof(int), typeof(LightBarPreview), new PropertyMetadata(0, (d, _) => ((LightBarPreview)d).Invalidate()));

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
        var width = Math.Min(size.Width * 0.9, size.Height * 8);
        var height = Math.Min(size.Height * 0.28, width / 14);
        var left = (size.Width - width) / 2;
        var top = (size.Height - height) / 2;
        var zones = Math.Max(1, Source?.Zones.Count ?? 1);
        var segments = zones * SegmentsPerZone;
        var gap = height * 0.18;
        var segment = (width - gap * (zones - 1)) / segments;

        using var bar = new CanvasCommandList(session);
        using (var ds = bar.CreateDrawingSession())
        {
            for (var i = 0; i < segments; i++)
            {
                var zone = i / SegmentsPerZone;
                var x = left + i * segment + zone * gap;
                var color = SegmentColor(zone, (i + 0.5) / segments);
                ds.FillRoundedRectangle((float)x, (float)top, (float)(segment + 0.6), (float)height, (float)(height * 0.3), (float)(height * 0.3), color);
            }
        }

        // The bar lights the surface under it; draw the glow, then the bar.
        using var glow = new GaussianBlurEffect { Source = bar, BlurAmount = (float)(height * 1.2), BorderMode = EffectBorderMode.Soft };
        using var faded = new OpacityEffect { Source = glow, Opacity = IsLight ? 0.45f : 0.9f };
        session.DrawImage(faded);
        session.DrawImage(bar);
    }

    /// <summary>Colour at <paramref name="along"/> (0..1 from the bar's left end), in <paramref name="zone"/>.</summary>
    private Color SegmentColor(int zone, double along)
    {
        var off = IsLight ? Color.FromArgb(0x26, 0, 0, 0) : Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
        if (Source is not { } vm)
            return off;
        var brightness = vm.BrightnessFraction;
        if (brightness <= 0)
            return off;

        Color color;
        if (vm.IsStatic)
        {
            if (zone >= vm.Zones.Count || !vm.Zones[zone].On)
                return off;
            color = vm.Zones[zone].Color;
        }
        else if (!vm.EffectUsesColor || !vm.EffectColorChosen)
        {
            // Neon, Wave and the Infinity Mirror's own effects choose their colours (and some pick random ones).
            color = LightingColors.Hue(along);
        }
        else if (vm.Effect == LightingEffect.Meteor)
        {
            var fade = 1 - (along * 1.6 - Math.Floor(along * 1.6));
            return TemperatureScale.Lerp(off, vm.EffectColor, (0.35 + 0.65 * brightness) * fade * fade);
        }
        else
        {
            color = vm.EffectColor;
        }
        return TemperatureScale.Lerp(off, color, 0.35 + 0.65 * brightness);
    }
}
