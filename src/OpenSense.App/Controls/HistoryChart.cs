using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml;
using OpenSense.App.Helpers;
using Windows.Foundation;

namespace OpenSense.App.Controls;

/// <summary>
/// Area chart of recent temperatures (newest on the right), coloured by height on the temperature scale,
/// with an optional faint second series.
/// </summary>
public sealed partial class HistoryChart : CanvasElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(HistoryBuffer), typeof(HistoryChart), new PropertyMetadata(null, (d, _) => ((HistoryChart)d).Invalidate()));

    public static readonly DependencyProperty SecondaryProperty = DependencyProperty.Register(
        nameof(Secondary), typeof(HistoryBuffer), typeof(HistoryChart), new PropertyMetadata(null, (d, _) => ((HistoryChart)d).Invalidate()));

    public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
        nameof(Revision), typeof(int), typeof(HistoryChart), new PropertyMetadata(0, (d, _) => ((HistoryChart)d).Invalidate()));

    public HistoryBuffer? Series
    {
        get => (HistoryBuffer?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>Drawn as a thin line on a 0–100 scale (e.g. load %).</summary>
    public HistoryBuffer? Secondary
    {
        get => (HistoryBuffer?)GetValue(SecondaryProperty);
        set => SetValue(SecondaryProperty, value);
    }

    public int Revision
    {
        get => (int)GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public double Minimum { get; set; } = 25;

    public double Maximum { get; set; } = 100;

    protected override void OnDraw(CanvasDrawingSession session, Size size)
    {
        var w = (float)size.Width;
        var h = (float)size.Height;
        if (w < 4 || h < 4)
            return;

        for (var i = 1; i <= 3; i++)
        {
            var y = h * i / 4f;
            session.DrawLine(0, y, w, y, GridLine, 1);
        }

        if (Secondary is { Count: > 1 } secondary)
        {
            using var line = Path(session, secondary, w, h, 0, 100, close: false);
            if (line is not null)
                session.DrawGeometry(line, TextTertiary, 1.2f);
        }

        if (Series is not { Count: > 1 } series)
            return;

        using (var area = Path(session, series, w, h, Minimum, Maximum, close: true))
        {
            if (area is not null)
            {
                using var fill = Gradient(session, h, 0x55, fade: true);
                session.FillGeometry(area, fill);
            }
        }

        using (var line = Path(session, series, w, h, Minimum, Maximum, close: false))
        {
            if (line is not null)
            {
                using var style = new CanvasStrokeStyle { LineJoin = CanvasLineJoin.Round };
                using var stroke = Gradient(session, h, 0xFF, fade: false);
                session.DrawGeometry(line, stroke, 2f, style);
            }
        }
    }

    /// <summary>
    /// Top-to-bottom brush following the temperature scale by height, so the line shifts smoothly from
    /// cool to hot colours as it rises. With <paramref name="fade"/> it also fades out towards the bottom
    /// (for the area under the line).
    /// </summary>
    private CanvasLinearGradientBrush Gradient(ICanvasResourceCreator creator, float h, byte alpha, bool fade)
    {
        const int Samples = 12;
        var stops = new CanvasGradientStop[Samples + 1];
        for (var i = 0; i <= Samples; i++)
        {
            var position = i / (float)Samples;
            var color = TemperatureScale.ColorFor(Maximum - position * (Maximum - Minimum));
            stops[i] = new CanvasGradientStop
            {
                Position = position,
                Color = TemperatureScale.WithAlpha(color, (byte)(fade ? alpha * (1 - position) : alpha)),
            };
        }
        return new CanvasLinearGradientBrush(creator, stops) { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, h) };
    }

    /// <summary>Builds the series path; gaps (nulls) break the line.</summary>
    private static CanvasGeometry? Path(ICanvasResourceCreator creator, HistoryBuffer buffer, float w, float h, double min, double max, bool close)
    {
        using var builder = new CanvasPathBuilder(creator);
        var step = w / (buffer.Capacity - 1);
        var offset = (buffer.Capacity - buffer.Count) * step;
        var open = false;
        float firstX = 0, lastX = 0;
        var any = false;

        for (var i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] is not { } value)
            {
                if (open)
                {
                    Close(builder, close, firstX, lastX, h);
                    open = false;
                }
                continue;
            }
            var x = offset + i * step;
            var y = h - (float)Math.Clamp((value - min) / (max - min), 0, 1) * (h - 2) - 1;
            if (!open)
            {
                builder.BeginFigure(close ? new Vector2(x, h) : new Vector2(x, y));
                if (close)
                    builder.AddLine(x, y);
                firstX = x;
                open = true;
            }
            else
            {
                builder.AddLine(x, y);
            }
            lastX = x;
            any = true;
        }
        if (open)
            Close(builder, close, firstX, lastX, h);

        return any ? CanvasGeometry.CreatePath(builder) : null;
    }

    private static void Close(CanvasPathBuilder builder, bool close, float firstX, float lastX, float h)
    {
        if (close)
        {
            builder.AddLine(lastX, h);
            builder.AddLine(firstX, h);
            builder.EndFigure(CanvasFigureLoop.Closed);
        }
        else
        {
            builder.EndFigure(CanvasFigureLoop.Open);
        }
    }
}
