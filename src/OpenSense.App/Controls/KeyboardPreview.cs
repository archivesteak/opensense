using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OpenSense.App.ViewModels;
using OpenSense.Core.Hardware;
using OpenSense.Core.Lighting;
using Windows.Foundation;
using Windows.UI;

namespace OpenSense.App.Controls;

/// <summary>
/// A laptop keyboard lit by zones, drawn from the chosen layout. A keyboard on the embedded controller plays its effect as
/// the controller does (<see cref="KeyboardAnimation"/>): the frames it sends the keyboard, when it sends them, in the
/// colours it sends. Other zoned keyboards show a still likeness of their effect. Zone 4 is the keypad and the main block
/// is split in three, as Acer's software draws four zones; the light of two zones mixes over about a key where they meet.
/// </summary>
public sealed partial class KeyboardPreview : CanvasElement
{
    /// <summary>The keypad starts here, in key units across the layout.</summary>
    private const double PadLeft = 15.25;

    private const double MainWidth = 15;

    /// <summary>Where four zones meet: at about 30 % and 65 % of the main block (NitroSense's and PredatorSense's drawings), then the keypad.</summary>
    private static readonly double[] FourZoneEdges = [4.5, 9.75, PadLeft];

    /// <summary>How wide, in keys, the light of two zones mixes.</summary>
    private const double Blend = 1;

    /// <summary>After a stall (a busy UI thread) the preview catches up by at most this many runs, then goes on from there.</summary>
    private const int MaxCatchUp = 100;

    /// <summary>The keyboard doesn't take up new settings at once; the preview waits as long before it does.</summary>
    private static readonly TimeSpan HardwareDelay = TimeSpan.FromMilliseconds(30);

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(LightingViewModel), typeof(KeyboardPreview), new PropertyMetadata(null, (d, _) => ((KeyboardPreview)d).Update()));

    public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(
        nameof(Revision), typeof(int), typeof(KeyboardPreview), new PropertyMetadata(0, (d, _) => ((KeyboardPreview)d).UpdateLater()));

    private readonly KeyboardAnimation _animation = new();
    private readonly Stopwatch _clock = new();
    private long _runsDone;
    private bool _loaded;
    private bool _ticking;
    private RgbColor[] _drawn = [];

    /// <summary>When the last change is due on screen (a <see cref="Stopwatch"/> timestamp), while one waits.</summary>
    private long? _pendingAt;

    // The keys' shapes, kept while the size, layout and zone count stay the same.
    private readonly List<CanvasGeometry> _keyShapes = [];
    private CanvasGeometry? _shapes;
    private (Size Size, KeyboardLayout Layout, int Zones, CanvasDevice? Device) _shapesFor;

    public KeyboardPreview()
    {
        Loaded += (_, _) =>
        {
            _loaded = true;
            Update();
        };
        Unloaded += (_, _) =>
        {
            _loaded = false;
            _pendingAt = null;
            UpdateTicking();
            // The canvas goes with its device; shapes are made again for the next one.
            _shapes?.Dispose();
            _shapes = null;
            foreach (var shape in _keyShapes)
                shape.Dispose();
            _keyShapes.Clear();
        };
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => UpdateTicking());
    }

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

    /// <summary>The light is the embedded controller's keyboard, whose effects the preview plays.</summary>
    private bool Animated => Source is { Device.Backend: LightingBackendKind.EcKeyboard, IsKeyboard: true };

    private bool Shown => _loaded && Visibility == Visibility.Visible;

    /// <summary>
    /// A setting changed: shown after <see cref="HardwareDelay"/>, as the keyboard shows it. The wait is timed on the
    /// preview's own clock, to the millisecond (a dispatcher timer only fires every 15.6 ms or so).
    /// </summary>
    private void UpdateLater()
    {
        if (!Shown)
        {
            Update();
            return;
        }
        _pendingAt = Stopwatch.GetTimestamp() + HardwareDelay.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
        UpdateTicking();
    }

    /// <summary>
    /// Hands the settings to the controller's model, as the service sends them to the keyboard, and has the page's zone
    /// colours change with it.
    /// </summary>
    private void Update()
    {
        _pendingAt = null;
        Source?.ShowZones();
        if (Animated && Source?.Current is { } lighting)
        {
            _animation.Show(lighting, Source.Zones.Count);
            // Static goes out on the controller's next run and then stays.
            if (_animation.Effect == KeyboardEffect.Static)
                _animation.Advance(1);
        }
        UpdateTicking();
        Invalidate();
    }

    private void UpdateTicking()
    {
        if (_pendingAt is not null && !Shown)
        {
            Update(); // nobody is watching: no need to wait
            return;
        }
        var tick = Shown && (_pendingAt is not null || Animated && _animation.Effect is not (null or KeyboardEffect.Static));
        if (tick == _ticking)
            return;
        _ticking = tick;
        if (tick)
        {
            _clock.Restart();
            _runsDone = 0;
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            CompositionTarget.Rendering -= OnRendering;
            _clock.Stop();
        }
    }

    /// <summary>
    /// Runs the controller's effect up to now, one run per <see cref="KeyboardAnimation.TickLength"/>, handing it a waiting
    /// change between the runs where it fell due; redraws when a new frame went out.
    /// </summary>
    private void OnRendering(object? sender, object e)
    {
        var now = Stopwatch.GetTimestamp();
        if (_pendingAt is { } at && now >= at)
        {
            RunUntil(_clock.Elapsed - Stopwatch.GetElapsedTime(at, now));
            Update();
        }
        RunUntil(_clock.Elapsed);
    }

    private void RunUntil(TimeSpan elapsed)
    {
        var due = elapsed.Ticks / KeyboardAnimation.TickLength.Ticks;
        var runs = due - _runsDone;
        if (runs <= 0)
            return;
        _runsDone = due;
        // While only a change is waited for, a still light has nothing to run.
        if (!_ticking || !Animated || _animation.Effect is null or KeyboardEffect.Static)
            return;
        _animation.Advance((int)Math.Min(runs, MaxCatchUp));
        if (!_animation.Zones.SequenceEqual(_drawn))
            Invalidate();
    }

    protected override void OnDraw(CanvasDrawingSession session, Size size)
    {
        if (Source is not { } vm)
            return;
        var zones = Math.Clamp(vm.Zones.Count, 1, KeyboardAnimation.ZoneCount);
        // Four zones have the keypad as the fourth; fewer zones are a keyboard without one.
        var layout = KeyboardLayouts.Get(vm.Layout);
        var keys = zones == 4 ? layout.Keys : [.. layout.Keys.Where(k => k.Bounds.Left < PadLeft)];
        if (keys.Count == 0)
            return;
        var bounds = keys.Select(k => k.Bounds).Aggregate((a, b) =>
        {
            a.Union(b);
            return a;
        });
        var unit = Math.Min(size.Width / bounds.Width, size.Height / bounds.Height);
        var originX = (size.Width - bounds.Width * unit) / 2 - bounds.Left * unit;
        var originY = (size.Height - bounds.Height * unit) / 2 - bounds.Top * unit;
        var shapes = KeyShapes(session, keys, (size, vm.Layout, zones, session.Device), originX, originY, unit);
        var edges = zones == 4 ? FourZoneEdges : [.. Enumerable.Range(1, zones - 1).Select(i => i * MainWidth / zones)];
        using var light = LightBrush(session, vm, zones, edges, bounds, originX, unit);

        // The keys unlit, their light on top, and the light's glow around them.
        var off = IsLight ? Color.FromArgb(0x26, 0, 0, 0) : Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
        using var lit = new CanvasCommandList(session);
        using (var ds = lit.CreateDrawingSession())
            ds.FillGeometry(shapes, light);
        using var glow = new GaussianBlurEffect { Source = lit, BlurAmount = (float)(unit * 0.55), BorderMode = EffectBorderMode.Soft };
        using var faded = new OpacityEffect { Source = glow, Opacity = IsLight ? 0.35f : 0.8f };
        session.DrawImage(faded);
        session.FillGeometry(shapes, off);
        session.DrawImage(lit);
    }

    /// <summary>All the keys' outlines as one shape, placed in the control.</summary>
    private CanvasGeometry KeyShapes(CanvasDrawingSession session, IReadOnlyList<LayoutKey> keys,
        (Size, KeyboardLayout, int, CanvasDevice?) key, double originX, double originY, double unit)
    {
        if (_shapes is not null && _shapesFor == key)
            return _shapes;
        _shapes?.Dispose();
        foreach (var shape in _keyShapes)
            shape.Dispose();
        _keyShapes.Clear();

        var inset = unit * 0.07;
        var radius = (float)(unit * 0.12);
        foreach (var r in keys.SelectMany(k => k.Rects))
        {
            _keyShapes.Add(CanvasGeometry.CreateRoundedRectangle(session,
                (float)(originX + r.X * unit + inset), (float)(originY + r.Y * unit + inset),
                (float)Math.Max(0, r.Width * unit - inset * 2), (float)Math.Max(0, r.Height * unit - inset * 2), radius, radius));
        }
        _shapes = CanvasGeometry.CreateGroup(session, [.. _keyShapes]);
        _shapesFor = key;
        return _shapes;
    }

    /// <summary>
    /// The keyboard's light across its width: each zone's colour, mixing with the next over <see cref="Blend"/> keys
    /// around their edge.
    /// </summary>
    private CanvasLinearGradientBrush LightBrush(CanvasDrawingSession session, LightingViewModel vm, int zones, double[] edges, Rect bounds, double originX, double unit)
    {
        CanvasGradientStop[] stops;
        if (!Animated && !vm.IsStatic && (!vm.EffectUsesColor || !vm.EffectColorChosen) && vm.BrightnessFraction > 0)
        {
            // Rainbow effects of keyboards whose effects aren't played: the colours along the keyboard.
            var alpha = Intensity(vm);
            stops = [.. Enumerable.Range(0, 7).Select(i => new CanvasGradientStop { Position = i / 6f, Color = WithAlpha(LightingColors.Hue(i / 6.0), alpha) })];
        }
        else
        {
            var colors = Animated ? SentColors(zones) : LikenessColors(vm, zones);
            CanvasGradientStop Stop(double units, Color color) => new() { Position = (float)Math.Clamp((units - bounds.Left) / bounds.Width, 0, 1), Color = color };
            List<CanvasGradientStop> list = [Stop(bounds.Left, colors[0])];
            for (var i = 0; i < edges.Length; i++)
            {
                list.Add(Stop(edges[i] - Blend / 2, colors[i]));
                list.Add(Stop(edges[i] + Blend / 2, colors[i + 1]));
            }
            list.Add(Stop(bounds.Right, colors[^1]));
            stops = [.. list];
        }
        return new CanvasLinearGradientBrush(session, stops)
        {
            StartPoint = new((float)(originX + bounds.Left * unit), 0),
            EndPoint = new((float)(originX + bounds.Right * unit), 0),
        };
    }

    /// <summary>
    /// The zones as the controller last sent them. A byte is the share of full light (255): the colour at full strength,
    /// that opaque over the unlit key; black is an unlit key.
    /// </summary>
    private Color[] SentColors(int zones)
    {
        _drawn = [.. _animation.Zones];
        return [.. _drawn.Take(zones).Select(c =>
        {
            var peak = Math.Max(c.R, Math.Max(c.G, c.B));
            return peak == 0
                ? Colors.Transparent
                : Color.FromArgb((byte)peak, (byte)(c.R * 255 / peak), (byte)(c.G * 255 / peak), (byte)(c.B * 255 / peak));
        })];
    }

    /// <summary>A keyboard whose effects aren't played: its static colours, or the effect's colour everywhere.</summary>
    private static Color[] LikenessColors(LightingViewModel vm, int zones)
    {
        if (vm.BrightnessFraction <= 0)
            return [.. Enumerable.Repeat(Colors.Transparent, zones)];
        var alpha = Intensity(vm);
        if (vm.IsStatic)
            return [.. Enumerable.Range(0, zones).Select(i => i < vm.Zones.Count && vm.Zones[i].On ? WithAlpha(vm.Zones[i].Color, alpha) : Colors.Transparent)];
        return [.. Enumerable.Repeat(WithAlpha(vm.EffectColor, alpha), zones)];
    }

    /// <summary>A likeness is never shown dimmer than a third, so its colour stays clear.</summary>
    private static byte Intensity(LightingViewModel vm) => (byte)(255 * (0.35 + 0.65 * vm.BrightnessFraction));

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
