using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml;
using OpenSense.App.Helpers;
using OpenSense.App.ViewModels;
using OpenSense.Core.Lighting;
using Windows.Foundation;
using Windows.UI;

namespace OpenSense.App.Controls;

/// <summary>The laptop's lid with its lit logo (a plain emblem, not Acer's), as it will look.</summary>
public sealed partial class LogoPreview : CanvasElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(LightingViewModel), typeof(LogoPreview), new PropertyMetadata(null, (d, _) => ((LogoPreview)d).Invalidate()));

    public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
        nameof(Revision), typeof(int), typeof(LogoPreview), new PropertyMetadata(0, (d, _) => ((LogoPreview)d).Invalidate()));

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
        // The lid: a wide rounded panel.
        var lidHeight = size.Height * 0.92;
        var lidWidth = Math.Min(size.Width * 0.8, lidHeight * 1.55);
        var lid = new Rect((size.Width - lidWidth) / 2, (size.Height - lidHeight) / 2, lidWidth, lidHeight);
        var lidColor = IsLight ? Color.FromArgb(0x14, 0, 0, 0) : Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
        session.FillRoundedRectangle(lid, (float)(lidHeight * 0.06), (float)(lidHeight * 0.06), lidColor);

        // The emblem: a chevron over a bar, centred on the lid.
        var scale = (float)(lidHeight * 0.34);
        var center = new Vector2((float)(lid.X + lid.Width / 2), (float)(lid.Y + lid.Height / 2));
        using var builder = new CanvasPathBuilder(session);
        builder.BeginFigure(center + new Vector2(-1.0f, -0.55f) * scale);
        builder.AddLine(center + new Vector2(0, 0.35f) * scale);
        builder.AddLine(center + new Vector2(1.0f, -0.55f) * scale);
        builder.AddLine(center + new Vector2(0.62f, -0.55f) * scale);
        builder.AddLine(center + new Vector2(0, 0.02f) * scale);
        builder.AddLine(center + new Vector2(-0.62f, -0.55f) * scale);
        builder.EndFigure(CanvasFigureLoop.Closed);
        builder.BeginFigure(center + new Vector2(-0.34f, 0.5f) * scale);
        builder.AddLine(center + new Vector2(0.34f, 0.5f) * scale);
        builder.AddLine(center + new Vector2(0.26f, 0.64f) * scale);
        builder.AddLine(center + new Vector2(-0.26f, 0.64f) * scale);
        builder.EndFigure(CanvasFigureLoop.Closed);
        using var emblem = CanvasGeometry.CreatePath(builder);

        using var lit = new CanvasCommandList(session);
        using (var ds = lit.CreateDrawingSession())
        {
            if (Source is { IsStatic: false } effect && (!effect.EffectUsesColor || !effect.EffectColorChosen) && effect.BrightnessFraction > 0)
            {
                using var rainbow = new CanvasLinearGradientBrush(ds,
                    [.. Enumerable.Range(0, 7).Select(i => new CanvasGradientStop { Position = i / 6f, Color = Lit(LightingColors.Hue(i / 6.0)) })])
                {
                    StartPoint = center - new Vector2(scale, 0),
                    EndPoint = center + new Vector2(scale, 0),
                };
                ds.FillGeometry(emblem, rainbow);
            }
            else if (Source is { IsStatic: true, Zones.Count: > 1 } zoned)
            {
                // A logo of several LEDs (Darfon's cover logo): their colours side by side.
                var count = zoned.Zones.Count;
                using var zones = new CanvasLinearGradientBrush(ds,
                    [.. zoned.Zones.Select((z, i) => new CanvasGradientStop { Position = (i + 0.5f) / count, Color = Lit(z.On ? z.Color : Off) })])
                {
                    StartPoint = center - new Vector2(scale, 0),
                    EndPoint = center + new Vector2(scale, 0),
                };
                ds.FillGeometry(emblem, zones);
            }
            else
            {
                ds.FillGeometry(emblem, Lit(BaseColor()));
            }
        }

        using var glow = new GaussianBlurEffect { Source = lit, BlurAmount = scale * 0.22f, BorderMode = EffectBorderMode.Soft };
        using var faded = new OpacityEffect { Source = glow, Opacity = IsLight ? 0.4f : 0.85f };
        session.DrawImage(faded);
        session.DrawImage(lit);
    }

    private Color Off => IsLight ? Color.FromArgb(0x30, 0, 0, 0) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);

    private Color BaseColor()
    {
        if (Source is not { } vm)
            return Off;
        if (vm.IsStatic)
            return vm.Zones.FirstOrDefault() is { On: true } zone ? zone.Color : Off;
        return vm.EffectColor;
    }

    /// <summary><paramref name="color"/> at the chosen brightness (dim, not black, at the lowest step).</summary>
    private Color Lit(Color color)
    {
        var brightness = Source?.BrightnessFraction ?? 1;
        return brightness <= 0 || color == Off ? Off : TemperatureScale.Lerp(Off, color, 0.35 + 0.65 * brightness);
    }
}
