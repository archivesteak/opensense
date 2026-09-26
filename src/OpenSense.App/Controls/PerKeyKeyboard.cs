using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using OpenSense.App.Helpers;
using OpenSense.App.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace OpenSense.App.Controls;

/// <summary>
/// A keyboard with a light per key, drawn from the chosen layout: in per-key mode keys are picked by clicking (Ctrl adds or
/// removes one) or dragging across them; in the other modes it previews the colours. Keys the layout has and the keyboard
/// lacks are drawn faint; keys the keyboard has that no layout places (Power, PredatorSense, media keys) get a row of
/// their own above the others. With only a few keys (the MagForce keys) just those are drawn.
/// </summary>
public sealed partial class PerKeyKeyboard : CanvasElement
{
    /// <summary>With no more keys than this the keyboard is a cluster, drawn on its own.</summary>
    private const int ClusterKeys = 12;

    private const double ExtraRowHeight = 0.65, ExtraRowGap = 0.2, ExtraKeyWidth = 1.25;

    /// <summary>The keypad starts here: it is drawn only when the keyboard has a key in it.</summary>
    private const double PadLeft = 15.25;

    private static readonly Dictionary<string, string> ExtraLegends = new()
    {
        ["Power"] = "⏻", ["PredatorSense"] = "PS", ["MyKey"] = "My",
        ["AudioVolumeMute"] = "Mute", ["AudioVolumeDown"] = "Vol−", ["AudioVolumeUp"] = "Vol+",
        ["MediaTrackPrevious"] = "⏮", ["MediaPlayPause"] = "⏯", ["MediaTrackNext"] = "⏭",
        ["PageDown"] = "PgDn", ["End"] = "End",
    };

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(LightingViewModel), typeof(PerKeyKeyboard), new PropertyMetadata(null, (d, _) => ((PerKeyKeyboard)d).Invalidate()));

    public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
        nameof(Revision), typeof(int), typeof(PerKeyKeyboard), new PropertyMetadata(0, (d, _) => ((PerKeyKeyboard)d).Invalidate()));

    // Where each key was last drawn, in the control's coordinates, for hit testing.
    private readonly List<(string Code, Rect[] Rects)> _hits = [];
    private bool _dragging;

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
        _hits.Clear();
        if (Source is not { Device: { } device } vm)
            return;

        var deviceKeys = device.Keys.ToHashSet();
        var layout = KeyboardLayouts.Get(vm.Layout);
        List<(string Code, string Legend, Rect[] Rects, bool Present)> keys;
        if (deviceKeys.Count <= ClusterKeys)
        {
            keys = [.. layout.Keys.Where(k => deviceKeys.Contains(k.Code)).Select(k => (k.Code, k.Legend, k.Rects.ToArray(), true))];
        }
        else
        {
            var hasPad = layout.Keys.Any(k => k.Bounds.Left >= PadLeft && deviceKeys.Contains(k.Code));
            keys = [.. layout.Keys
                .Where(k => hasPad || k.Bounds.Left < PadLeft || deviceKeys.Contains(k.Code))
                .Select(k => (k.Code, k.Legend, k.Rects.ToArray(), deviceKeys.Contains(k.Code)))];
            var extras = device.Keys.Where(k => !KeyboardLayouts.OnAnyLayout(k)).ToList();
            for (var i = 0; i < extras.Count; i++)
            {
                var rect = new Rect(i * ExtraKeyWidth, -(ExtraRowHeight + ExtraRowGap), ExtraKeyWidth, ExtraRowHeight);
                keys.Add((extras[i], ExtraLegends.GetValueOrDefault(extras[i], extras[i]), [rect], true));
            }
        }
        if (keys.Count == 0)
            return;

        var bounds = keys.SelectMany(k => k.Rects).Aggregate((a, b) =>
        {
            a.Union(b);
            return a;
        });
        var unit = Math.Min(size.Width / bounds.Width, size.Height / bounds.Height);
        if (deviceKeys.Count <= ClusterKeys)
            unit = Math.Min(unit, size.Height / 3.2);
        var originX = (size.Width - bounds.Width * unit) / 2 - bounds.Left * unit;
        var originY = (size.Height - bounds.Height * unit) / 2 - bounds.Top * unit;
        Rect Place(Rect r) => new(originX + r.X * unit, originY + r.Y * unit, r.Width * unit, r.Height * unit);

        var inset = unit * 0.07;
        var radius = (float)(unit * 0.12);
        using var format = new CanvasTextFormat
        {
            FontFamily = "Segoe UI",
            FontSize = (float)Math.Max(7, unit * 0.26),
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            WordWrapping = CanvasWordWrapping.NoWrap,
            Options = CanvasDrawTextOptions.Clip,
        };
        var off = IsLight ? Color.FromArgb(0x26, 0, 0, 0) : Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
        var faint = IsLight ? Color.FromArgb(0x30, 0, 0, 0) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
        var selection = Accent;
        var interactive = vm.IsPerKey;

        foreach (var (code, legend, rects, present) in keys)
        {
            var placed = rects.Select(r =>
            {
                var p = Place(r);
                return new Rect(p.X + inset, p.Y + inset, Math.Max(0, p.Width - inset * 2), Math.Max(0, p.Height - inset * 2));
            }).ToArray();
            if (!present)
            {
                foreach (var r in placed)
                    session.DrawRoundedRectangle(r, radius, radius, faint, 1);
                continue;
            }

            var along = (rects[0].X + rects[0].Width / 2 - bounds.Left) / bounds.Width;
            var color = KeyColor(vm, code, along) is { } lit ? lit : off;
            foreach (var r in placed)
                session.FillRoundedRectangle(r, radius, radius, color);
            if (interactive && vm.IsKeySelected(code))
            {
                foreach (var r in placed)
                    session.DrawRoundedRectangle(r, radius, radius, selection, (float)Math.Max(2, unit * 0.06));
            }
            if (legend.Length > 0)
                session.DrawText(legend, placed[0], LegendColor(color), format);
            _hits.Add((code, placed));
        }
    }

    /// <summary>A key's colour as the light will show it; null when it is dark.</summary>
    private static Color? KeyColor(LightingViewModel vm, string code, double along)
    {
        var brightness = vm.BrightnessFraction;
        if (brightness <= 0)
            return null;
        Color color;
        if (vm.IsPerKey)
        {
            if (vm.KeyColor(code) is not { } key)
                return null;
            color = key;
        }
        else if (vm.IsStatic)
        {
            if (vm.Zones.FirstOrDefault() is not { On: true } zone)
                return null;
            color = zone.Color;
        }
        else if (!vm.EffectUsesColor || !vm.EffectColorChosen)
        {
            color = LightingColors.Hue(along);
        }
        else
        {
            color = vm.EffectColor;
        }
        var dark = Color.FromArgb(0xFF, 0x20, 0x20, 0x20);
        return TemperatureScale.Lerp(dark, color, 0.35 + 0.65 * brightness);
    }

    private Color LegendColor(Color key)
    {
        if (key.A < 0x80)
            return TextSecondary;
        var luminance = (0.299 * key.R + 0.587 * key.G + 0.114 * key.B) / 255;
        return luminance > 0.6 ? Color.FromArgb(0xD0, 0, 0, 0) : Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF);
    }

    protected override void AttachInput(CanvasControl canvas)
    {
        canvas.PointerPressed += OnPointerPressed;
        canvas.PointerMoved += OnPointerMoved;
        canvas.PointerReleased += (_, _) => _dragging = false;
        canvas.PointerCaptureLost += (_, _) => _dragging = false;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Source is not { IsPerKey: true } vm || sender is not CanvasControl canvas)
            return;
        var point = e.GetCurrentPoint(canvas).Position;
        if (Hit(point) is not { } code)
        {
            if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
                vm.ClearKeySelectionCommand.Execute(null);
            return;
        }
        vm.SelectKey(code, toggle: e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control));
        _dragging = canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is CanvasControl canvas)
            ProtectedCursor = Source is { IsPerKey: true } && Hit(e.GetCurrentPoint(canvas).Position) is not null
                ? InputSystemCursor.Create(InputSystemCursorShape.Hand)
                : null;
        if (!_dragging || Source is not { IsPerKey: true } vm || sender is not CanvasControl control)
            return;
        if (Hit(e.GetCurrentPoint(control).Position) is { } code)
            vm.AddKeyToSelection(code);
    }

    private string? Hit(Point point) => _hits.FirstOrDefault(h => h.Rects.Any(r => r.Contains(point))).Code;
}
