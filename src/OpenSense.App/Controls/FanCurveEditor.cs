using System.Globalization;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OpenSense.App.Helpers;
using OpenSense.App.Localization;
using OpenSense.Core.Control;
using Windows.Foundation;
using Windows.System;

namespace OpenSense.App.Controls;

/// <summary>
/// Interactive temperature → fan duty curve. Drag points, double-click to add, right-click to remove,
/// arrow keys to nudge the selected point (Shift for bigger steps).
/// </summary>
public sealed partial class FanCurveEditor : CanvasElement
{
    private const float PadLeft = 44, PadRight = 18, PadTop = 14, PadBottom = 30;
    private const float HitRadius = 14;
    private const int MinPoints = 2;

    private List<CurvePoint> _points = [];
    private int _dragging = -1;
    private int _hover = -1;
    private readonly PointerEventHandler _pressedAnywhere;
    private UIElement? _window;

    public FanCurveEditor()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        KeyDown += OnKeyDown;
        _pressedAnywhere = OnPressedAnywhere;

        // A selected point (and its value tag) stays until the user clicks anything else or tabs away.
        LostFocus += (_, _) => Deselect();
        Loaded += (_, _) =>
        {
            _window = XamlRoot?.Content;
            _window?.AddHandler(PointerPressedEvent, _pressedAnywhere, handledEventsToo: true);
        };
        Unloaded += (_, _) =>
        {
            _window?.RemoveHandler(PointerPressedEvent, _pressedAnywhere);
            _window = null;
        };
    }

    public static readonly DependencyProperty CurveProperty = DependencyProperty.Register(
        nameof(Curve), typeof(FanCurve), typeof(FanCurveEditor), new PropertyMetadata(null, (d, e) => ((FanCurveEditor)d).OnCurveChanged((FanCurve?)e.NewValue)));

    public static readonly DependencyProperty LiveTemperatureProperty = DependencyProperty.Register(
        nameof(LiveTemperature), typeof(double), typeof(FanCurveEditor), new PropertyMetadata(double.NaN, (d, _) => ((FanCurveEditor)d).Invalidate()));

    public static readonly DependencyProperty LivePercentProperty = DependencyProperty.Register(
        nameof(LivePercent), typeof(double), typeof(FanCurveEditor), new PropertyMetadata(double.NaN, (d, _) => ((FanCurveEditor)d).Invalidate()));

    public static readonly DependencyProperty UseFahrenheitProperty = DependencyProperty.Register(
        nameof(UseFahrenheit), typeof(bool), typeof(FanCurveEditor), new PropertyMetadata(false, (d, _) => ((FanCurveEditor)d).Invalidate()));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(FanCurveEditor), new PropertyMetadata(-1, (d, _) => ((FanCurveEditor)d).Invalidate()));

    public FanCurve? Curve
    {
        get => (FanCurve?)GetValue(CurveProperty);
        set => SetValue(CurveProperty, value);
    }

    /// <summary>Current source temperature in °C (NaN hides the live marker).</summary>
    public double LiveTemperature
    {
        get => (double)GetValue(LiveTemperatureProperty);
        set => SetValue(LiveTemperatureProperty, value);
    }

    /// <summary>Duty the fan is actually running at, in %.</summary>
    public double LivePercent
    {
        get => (double)GetValue(LivePercentProperty);
        set => SetValue(LivePercentProperty, value);
    }

    public bool UseFahrenheit
    {
        get => (bool)GetValue(UseFahrenheitProperty);
        set => SetValue(UseFahrenheitProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private void OnCurveChanged(FanCurve? curve)
    {
        if (_dragging < 0)
            _points = curve is null ? [] : [.. curve.Points];
        Invalidate();
    }

    // --- geometry ---------------------------------------------------------------------------

    private static Rect Plot(Size size) =>
        new(PadLeft, PadTop, Math.Max(1, size.Width - PadLeft - PadRight), Math.Max(1, size.Height - PadTop - PadBottom));

    private static float X(Rect plot, double celsius) =>
        (float)(plot.X + (celsius - FanCurve.MinTemperature) / (FanCurve.MaxTemperature - FanCurve.MinTemperature) * plot.Width);

    private static float Y(Rect plot, double percent) => (float)(plot.Y + plot.Height - percent / 100 * plot.Height);

    private static double TemperatureAt(Rect plot, double x) =>
        FanCurve.MinTemperature + (x - plot.X) / plot.Width * (FanCurve.MaxTemperature - FanCurve.MinTemperature);

    private static double PercentAt(Rect plot, double y) => (plot.Y + plot.Height - y) / plot.Height * 100;

    private Size CanvasSize => Canvas is { } c ? c.Size : new Size(ActualWidth, ActualHeight);

    // --- drawing ----------------------------------------------------------------------------

    protected override void OnDraw(CanvasDrawingSession session, Size size)
    {
        var plot = Plot(size);
        // Tags hold words: shape and order them in the app's language.
        using var labels = new CanvasTextFormat
        {
            FontSize = 11,
            FontFamily = "Segoe UI Variable Text",
            LocaleName = AppLanguage.Current,
            Direction = AppLanguage.IsRightToLeft ? CanvasTextDirection.RightToLeftThenTopToBottom : CanvasTextDirection.LeftToRightThenTopToBottom,
        };
        using var rightLabels = new CanvasTextFormat { FontSize = 11, FontFamily = "Segoe UI Variable Text", HorizontalAlignment = CanvasHorizontalAlignment.Right };
        using var centered = new CanvasTextFormat { FontSize = 11, FontFamily = "Segoe UI Variable Text", HorizontalAlignment = CanvasHorizontalAlignment.Center };

        // Grid and axis labels.
        for (var pct = 0; pct <= 100; pct += 20)
        {
            var y = Y(plot, pct);
            session.DrawLine((float)plot.X, y, (float)plot.Right, y, GridLine, 1);
            session.DrawText(Units.Percent(pct), (float)plot.X - 8, y - 8, TextTertiary, rightLabels);
        }
        var labelEvery = plot.Width > 420 ? 10 : 20;
        for (var t = FanCurve.MinTemperature; t <= FanCurve.MaxTemperature; t += 10)
        {
            var x = X(plot, t);
            session.DrawLine(x, (float)plot.Y, x, (float)plot.Bottom, GridLine, 1);
            if ((t - FanCurve.MinTemperature) % labelEvery == 0)
                session.DrawText(string.Create(CultureInfo.CurrentCulture, $"{Units.ToDisplay(t, UseFahrenheit):0}°"), x, (float)plot.Bottom + 8, TextTertiary, centered);
        }

        if (_points.Count == 0)
            return;

        // Curve, flat beyond the first and last point.
        using var line = BuildLine(session, plot, close: false);
        using var area = BuildLine(session, plot, close: true);
        using (var fill = new CanvasLinearGradientBrush(session, TemperatureScale.WithAlpha(Accent, 0x50), TemperatureScale.WithAlpha(Accent, 0x04))
               {
                   StartPoint = new Vector2(0, (float)plot.Y),
                   EndPoint = new Vector2(0, (float)plot.Bottom),
               })
        {
            session.FillGeometry(area, fill);
        }
        using (var style = new CanvasStrokeStyle { LineJoin = CanvasLineJoin.Round })
            session.DrawGeometry(line, Accent, 2.5f, style);

        DrawLiveMarker(session, plot, labels);

        // Points.
        for (var i = 0; i < _points.Count; i++)
        {
            var center = new Vector2(X(plot, _points[i].Temperature), Y(plot, _points[i].Percent));
            var active = i == _dragging || i == _hover || i == SelectedIndex;
            var radius = active ? 7.5f : 6f;
            session.FillCircle(center, radius, active ? Accent : Surface);
            session.DrawCircle(center, radius, Accent, 2.5f);
        }

        // Value tag while dragging or for the selected point.
        var tagIndex = _dragging >= 0 ? _dragging : SelectedIndex;
        if (tagIndex >= 0 && tagIndex < _points.Count)
        {
            var p = _points[tagIndex];
            DrawTag(session, labels, new Vector2(X(plot, p.Temperature), Y(plot, p.Percent) - 26),
                $"{Units.TemperatureWithUnit(p.Temperature, UseFahrenheit)} · {Units.Percent(p.Percent)}", plot);
        }
    }

    private CanvasGeometry BuildLine(ICanvasResourceCreator creator, Rect plot, bool close)
    {
        using var builder = new CanvasPathBuilder(creator);
        var first = _points[0];
        var start = new Vector2((float)plot.X, Y(plot, first.Percent));
        builder.BeginFigure(close ? new Vector2((float)plot.X, (float)plot.Bottom) : start);
        if (close)
            builder.AddLine(start);
        foreach (var p in _points)
            builder.AddLine(X(plot, p.Temperature), Y(plot, p.Percent));
        builder.AddLine((float)plot.Right, Y(plot, _points[^1].Percent));
        if (close)
        {
            builder.AddLine((float)plot.Right, (float)plot.Bottom);
            builder.EndFigure(CanvasFigureLoop.Closed);
        }
        else
        {
            builder.EndFigure(CanvasFigureLoop.Open);
        }
        return CanvasGeometry.CreatePath(builder);
    }

    private void DrawLiveMarker(CanvasDrawingSession session, Rect plot, CanvasTextFormat format)
    {
        if (double.IsNaN(LiveTemperature))
            return;
        var temperature = Math.Clamp(LiveTemperature, FanCurve.MinTemperature, FanCurve.MaxTemperature);
        var percent = double.IsNaN(LivePercent) ? new FanCurve { Points = _points }.Evaluate(temperature) : LivePercent;
        var x = X(plot, temperature);
        var y = Y(plot, percent);
        var color = TemperatureScale.ColorFor(LiveTemperature);
        using var dashed = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        session.DrawLine(x, (float)plot.Bottom, x, y, TemperatureScale.WithAlpha(color, 0xC0), 1.5f, dashed);
        session.FillCircle(x, y, 9, TemperatureScale.WithAlpha(color, 0x40));
        session.FillCircle(x, y, 4.5f, color);
        // Just above the point it labels; below it where the curve runs along the top.
        var tagTop = y - 34 >= plot.Y - 10 ? y - 34 : y + 12;
        DrawTag(session, format, new Vector2(x, tagTop),
            Strings.Format("CurveEditor_Now", Units.Temperature(LiveTemperature, UseFahrenheit), Units.Percent(percent)), plot, color);
    }

    private void DrawTag(CanvasDrawingSession session, CanvasTextFormat format, Vector2 anchor, string text, Rect plot, Windows.UI.Color? accent = null)
    {
        using var layout = new CanvasTextLayout(session, text, format, 400, 40);
        var width = (float)layout.LayoutBounds.Width + 16;
        var height = (float)layout.LayoutBounds.Height + 8;
        var left = Math.Clamp(anchor.X - width / 2, (float)plot.X, (float)plot.Right - width);
        var top = Math.Max((float)plot.Y - 10, anchor.Y);
        session.FillRoundedRectangle(left, top, width, height, 6, 6, Surface);
        session.DrawRoundedRectangle(left, top, width, height, 6, 6, accent ?? Accent, 1);
        session.DrawTextLayout(layout, left + 8, top + 4, TextPrimary);
    }

    // --- input ------------------------------------------------------------------------------

    protected override void AttachInput(CanvasControl canvas)
    {
        canvas.PointerPressed += OnPointerPressed;
        canvas.PointerMoved += OnPointerMoved;
        canvas.PointerReleased += OnPointerReleased;
        canvas.PointerCaptureLost += (_, _) => EndDrag();
        canvas.PointerExited += (_, _) =>
        {
            if (_dragging < 0 && _hover >= 0)
            {
                _hover = -1;
                Invalidate();
            }
        };
        canvas.DoubleTapped += OnDoubleTapped;
        canvas.RightTapped += OnRightTapped;
    }

    private int HitTest(Point position)
    {
        var plot = Plot(CanvasSize);
        for (var i = 0; i < _points.Count; i++)
        {
            var dx = position.X - X(plot, _points[i].Temperature);
            var dy = position.Y - Y(plot, _points[i].Percent);
            if (dx * dx + dy * dy <= HitRadius * HitRadius)
                return i;
        }
        return -1;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsEnabled)
            return;
        Focus(FocusState.Pointer);
        var hit = HitTest(e.GetCurrentPoint(Canvas).Position);
        if (hit < 0)
        {
            Deselect();
            return;
        }
        _dragging = hit;
        SelectedIndex = hit;
        Canvas?.CapturePointer(e.Pointer);
        e.Handled = true;
        Invalidate();
    }

    private void OnPressedAnywhere(object sender, PointerRoutedEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element == this)
                return;
        }
        Deselect();
    }

    private void Deselect()
    {
        if (SelectedIndex >= 0 && _dragging < 0)
            SelectedIndex = -1;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(Canvas).Position;
        if (_dragging < 0)
        {
            var hover = IsEnabled ? HitTest(position) : -1;
            if (hover != _hover)
            {
                _hover = hover;
                ProtectedCursor = InputSystemCursor.Create(hover >= 0 ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Arrow);
                Invalidate();
            }
            return;
        }

        var plot = Plot(CanvasSize);
        MovePoint(_dragging, TemperatureAt(plot, position.X), PercentAt(plot, position.Y), commit: false);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging < 0)
            return;
        Canvas?.ReleasePointerCapture(e.Pointer);
        EndDrag();
    }

    private void EndDrag()
    {
        if (_dragging < 0)
            return;
        _dragging = -1;
        Commit();
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!IsEnabled || _points.Count >= FanCurve.MaxPoints || HitTest(e.GetPosition(Canvas)) >= 0)
            return;
        var plot = Plot(CanvasSize);
        var position = e.GetPosition(Canvas);
        var temperature = (int)Math.Round(Math.Clamp(TemperatureAt(plot, position.X), FanCurve.MinTemperature, FanCurve.MaxTemperature));
        if (_points.Any(p => p.Temperature == temperature))
            return;
        var percent = (int)Math.Round(Math.Clamp(PercentAt(plot, position.Y), 0, 100));
        _points = [.. _points.Append(new CurvePoint(temperature, percent)).OrderBy(p => p.Temperature)];
        SelectedIndex = _points.FindIndex(p => p.Temperature == temperature);
        Commit();
    }

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var hit = HitTest(e.GetPosition(Canvas));
        if (!IsEnabled || hit < 0 || _points.Count <= MinPoints)
            return;
        _points.RemoveAt(hit);
        SelectedIndex = -1;
        e.Handled = true;
        Commit();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (SelectedIndex < 0 || SelectedIndex >= _points.Count)
        {
            if (e.Key is VirtualKey.Left or VirtualKey.Right && _points.Count > 0)
            {
                SelectedIndex = e.Key == VirtualKey.Left ? _points.Count - 1 : 0;
                e.Handled = true;
            }
            return;
        }

        var step = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) ? 5 : 1;
        var p = _points[SelectedIndex];
        switch (e.Key)
        {
            case VirtualKey.Left: MovePoint(SelectedIndex, p.Temperature - step, p.Percent, commit: true); break;
            case VirtualKey.Right: MovePoint(SelectedIndex, p.Temperature + step, p.Percent, commit: true); break;
            case VirtualKey.Up: MovePoint(SelectedIndex, p.Temperature, p.Percent + step, commit: true); break;
            case VirtualKey.Down: MovePoint(SelectedIndex, p.Temperature, p.Percent - step, commit: true); break;
            case VirtualKey.Tab when _points.Count > 1:
                SelectedIndex = (SelectedIndex + 1) % _points.Count;
                break;
            default: return;
        }
        e.Handled = true;
    }

    /// <summary>Moves a point, keeping it between its neighbours (1 °C apart) and inside the chart.</summary>
    private void MovePoint(int index, double temperature, double percent, bool commit)
    {
        var low = index > 0 ? _points[index - 1].Temperature + 1 : FanCurve.MinTemperature;
        var high = index < _points.Count - 1 ? _points[index + 1].Temperature - 1 : FanCurve.MaxTemperature;
        _points[index] = new CurvePoint(
            (int)Math.Round(Math.Clamp(temperature, low, high)),
            (int)Math.Round(Math.Clamp(percent, 0, 100)));
        if (commit)
            Commit();
        else
            Invalidate();
    }

    private void Commit()
    {
        Curve = new FanCurve { Points = [.. _points] };
        Invalidate();
    }
}
