using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace OpenSense.App.Controls;

/// <summary>
/// A blower fan whose impeller spins at a speed derived from the RPM and smears as it speeds up,
/// inside a ring that shows the duty cycle. The spin runs on the compositor thread; the canvases
/// only redraw when the speed or duty changes.
/// </summary>
public sealed partial class FanRotor : CanvasElement
{
    /// <summary>On-screen turns per real turn: a fan at 3,000 rpm really turns 50 times a second.</summary>
    private const double VisualScale = 0.022;
    private const double MinTurnsPerSecond = 0.15;
    private const int FullSpeedRpm = 5500;
    private const float ImpellerScale = 0.76f;
    private const string SpinProperty = nameof(Visual.RotationAngleInDegrees);
    private static readonly TimeSpan RampLength = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan DutyAnimationLength = TimeSpan.FromMilliseconds(450);

    private readonly Impeller _impeller = new();
    private readonly Visual _visual;
    private readonly DispatcherQueueTimer _dutyTimer;
    private bool _animate = true;

    // The impeller eases from _fromSpeed to _toSpeed (turns per second), starting at _rampStart.
    private double _fromSpeed, _toSpeed;
    private DateTime _rampStart = DateTime.MinValue;
    private int _generation;

    private double _shownDuty = double.NaN, _dutyFrom, _dutyTo;
    private DateTime _dutyStart;

    public FanRotor()
    {
        _impeller.HorizontalAlignment = HorizontalAlignment.Center;
        _impeller.VerticalAlignment = VerticalAlignment.Center;
        Root.Children.Add(_impeller);

        _visual = ElementCompositionPreview.GetElementVisual(_impeller);
        _impeller.SizeChanged += (_, e) => _visual.CenterPoint = new Vector3((float)e.NewSize.Width / 2, (float)e.NewSize.Height / 2, 0);
        SizeChanged += (_, e) => _impeller.Width = _impeller.Height = Math.Min(e.NewSize.Width, e.NewSize.Height) * ImpellerScale;

        _dutyTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _dutyTimer.Interval = TimeSpan.FromMilliseconds(16);
        _dutyTimer.Tick += (_, _) => StepDuty();

        Loaded += (_, _) =>
        {
            _animate = new UISettings().AnimationsEnabled;
            SpinTo(TurnsPerSecond(Rpm), force: true);
        };
        Unloaded += (_, _) =>
        {
            _generation++;
            _visual.StopAnimation(SpinProperty);
            _fromSpeed = _toSpeed = 0;
            _rampStart = DateTime.MinValue;
            _dutyTimer.Stop();
        };
    }

    public static readonly DependencyProperty RpmProperty = DependencyProperty.Register(
        nameof(Rpm), typeof(int), typeof(FanRotor), new PropertyMetadata(0, (d, _) => ((FanRotor)d).OnRpmChanged()));

    public static readonly DependencyProperty DutyProperty = DependencyProperty.Register(
        nameof(Duty), typeof(double), typeof(FanRotor), new PropertyMetadata(double.NaN, (d, _) => ((FanRotor)d).AnimateDuty()));

    public int Rpm
    {
        get => (int)GetValue(RpmProperty);
        set => SetValue(RpmProperty, value);
    }

    /// <summary>Duty cycle in percent; NaN when unknown.</summary>
    public double Duty
    {
        get => (double)GetValue(DutyProperty);
        set => SetValue(DutyProperty, value);
    }

    private static double TurnsPerSecond(int rpm) => rpm <= 0 ? 0 : Math.Max(MinTurnsPerSecond, rpm / 60.0 * VisualScale);

    private void OnRpmChanged()
    {
        var speed = TurnsPerSecond(Rpm);
        _impeller.SetSpeed(speed, Math.Clamp(Rpm / (double)FullSpeedRpm, 0, 1));
        if (IsLoaded)
            SpinTo(speed);
    }

    /// <summary>
    /// Eases the spin to a new speed. The ramp is a cubic Bézier over linear time whose end slopes
    /// equal the old and new speeds, so the impeller neither jumps nor changes speed abruptly.
    /// </summary>
    private void SpinTo(double target, bool force = false)
    {
        if (!_animate)
        {
            _visual.StopAnimation(SpinProperty);
            _fromSpeed = _toSpeed = target;
            return;
        }
        if (!force && Math.Abs(target - _toSpeed) < 0.005)
            return;

        var now = DateTime.UtcNow;
        var current = CurrentSpeed(now);
        _fromSpeed = current;
        _toSpeed = target;
        _rampStart = now;
        var generation = ++_generation;

        var sum = current + target;
        if (sum <= 0)
        {
            _visual.StopAnimation(SpinProperty);
            return;
        }

        var compositor = _visual.Compositor;
        var degrees = sum / 2 * RampLength.TotalSeconds * 360;
        var easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(1 / 3f, (float)(2 * current / (3 * sum))),
            new Vector2(2 / 3f, (float)(1 - 2 * target / (3 * sum))));
        var ramp = compositor.CreateScalarKeyFrameAnimation();
        ramp.InsertExpressionKeyFrame(0, "Mod(this.StartingValue, 360)");
        ramp.InsertExpressionKeyFrame(1, FormattableString.Invariant($"Mod(this.StartingValue, 360) + {degrees:0.###}"), easing);
        ramp.Duration = RampLength;

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        _visual.StartAnimation(SpinProperty, ramp);
        batch.End();
        batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            if (generation == _generation)
                Cruise(target);
        });
    }

    private void Cruise(double speed)
    {
        if (speed <= 0)
            return; // the ramp has come to rest and keeps its final angle

        var compositor = _visual.Compositor;
        var loop = compositor.CreateScalarKeyFrameAnimation();
        loop.InsertExpressionKeyFrame(0, "Mod(this.StartingValue, 360)");
        loop.InsertExpressionKeyFrame(1, "Mod(this.StartingValue, 360) + 360", compositor.CreateLinearEasingFunction());
        loop.Duration = TimeSpan.FromSeconds(1 / speed);
        loop.IterationBehavior = AnimationIterationBehavior.Forever;
        _visual.StartAnimation(SpinProperty, loop);
    }

    /// <summary>The speed the impeller is turning at now, following the eased ramp in <see cref="SpinTo"/>.</summary>
    private double CurrentSpeed(DateTime now)
    {
        var t = (now - _rampStart) / RampLength;
        var sum = _fromSpeed + _toSpeed;
        if (t >= 1 || sum <= 0)
            return _toSpeed;
        var y1 = 2 * _fromSpeed / (3 * sum);
        var y2 = 1 - 2 * _toSpeed / (3 * sum);
        var slope = 3 * (1 - t) * (1 - t) * y1 + 6 * (1 - t) * t * (y2 - y1) + 3 * t * t * (1 - y2);
        return slope * sum / 2;
    }

    private void AnimateDuty()
    {
        if (double.IsNaN(Duty) || double.IsNaN(_shownDuty) || !_animate)
        {
            _shownDuty = Duty;
            Invalidate();
            return;
        }
        _dutyFrom = _shownDuty;
        _dutyTo = Duty;
        _dutyStart = DateTime.UtcNow;
        _dutyTimer.Start();
    }

    private void StepDuty()
    {
        var t = Math.Min(1, (DateTime.UtcNow - _dutyStart) / DutyAnimationLength);
        _shownDuty = _dutyFrom + (_dutyTo - _dutyFrom) * (1 - Math.Pow(1 - t, 3));
        if (t >= 1)
            _dutyTimer.Stop();
        Invalidate();
    }

    protected override void OnDraw(CanvasDrawingSession session, Size size)
    {
        var diameter = (float)Math.Min(size.Width, size.Height);
        var stroke = Math.Max(3f, diameter * 0.025f);
        var radius = diameter / 2 - stroke;
        var center = new Vector2((float)size.Width / 2, (float)size.Height / 2);

        session.DrawCircle(center, radius, Track, stroke);
        session.DrawCircle(center, radius - stroke * 2.5f, GridLine, 1);

        if (double.IsNaN(_shownDuty) || _shownDuty <= 0)
            return;
        var fraction = (float)Math.Clamp(_shownDuty / 100, 0, 1);
        if (fraction >= 0.999f)
        {
            session.DrawCircle(center, radius, Accent, stroke);
            return;
        }
        using var builder = new CanvasPathBuilder(session);
        builder.BeginFigure(center + new Vector2(0, -radius));
        builder.AddArc(center, radius, radius, -MathF.PI / 2, MathF.Tau * fraction);
        builder.EndFigure(CanvasFigureLoop.Open);
        using var arc = CanvasGeometry.CreatePath(builder);
        using var round = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
        session.DrawGeometry(arc, Accent, stroke, round);
    }

    private static Color WithOpacity(Color c, float opacity) => Color.FromArgb((byte)(c.A * opacity), c.R, c.G, c.B);

    private static Vector2 Polar(float radius, float angle) => new(radius * MathF.Cos(angle), radius * MathF.Sin(angle));

    /// <summary>The spinning part: curved blades around an accent hub, drawn once per speed.</summary>
    private sealed partial class Impeller : CanvasElement
    {
        private const int Blades = 9;
        private const float Sweep = 0.6f;
        private const float Thickness = 0.13f;

        private float _smear;
        private float _level;

        /// <summary>Faster fans smear their blades further (motion blur) and fill in more of the disc.</summary>
        public void SetSpeed(double turnsPerSecond, double level)
        {
            var smear = (float)Math.Min(MathF.Tau / Blades * 0.45, turnsPerSecond * Math.Tau * 0.025);
            if (Math.Abs(smear - _smear) < 0.005f && Math.Abs(level - _level) < 0.01)
                return;
            _smear = smear;
            _level = (float)level;
            Invalidate();
        }

        protected override void OnDraw(CanvasDrawingSession session, Size size)
        {
            var radius = (float)Math.Min(size.Width, size.Height) / 2;
            var center = new Vector2((float)size.Width / 2, (float)size.Height / 2);
            var hub = radius * 0.36f;

            if (_level > 0)
                session.FillCircle(center, radius, WithOpacity(TextPrimary, Math.Min(0.07f, _level * 0.08f)));

            // Trailing copies behind the direction of travel (clockwise) make the motion blur.
            var copies = _smear > 0.03f ? Math.Min(4, (int)MathF.Ceiling(_smear / 0.05f) + 1) : 1;
            for (var k = 0; k < copies; k++)
            {
                var offset = copies == 1 ? 0 : -_smear * k / (copies - 1);
                using var blades = BladeGeometry(session, center, hub * 0.96f, radius, offset);
                session.FillGeometry(blades, WithOpacity(TextSecondary, k == 0 ? 0.85f : 0.5f / copies));
            }

            session.FillCircle(center, hub, Surface);
            session.DrawCircle(center, hub, TextTertiary, 1);
            session.FillCircle(center, hub * 0.52f, Accent);
        }

        private static CanvasGeometry BladeGeometry(ICanvasResourceCreator creator, Vector2 center, float inner, float outer, float offset)
        {
            using var builder = new CanvasPathBuilder(creator);
            for (var i = 0; i < Blades; i++)
            {
                var a = offset + i * MathF.Tau / Blades;
                builder.BeginFigure(center + Polar(inner, a));
                builder.AddQuadraticBezier(center + Polar((inner + outer) * 0.52f, a + Sweep * 0.22f), center + Polar(outer, a + Sweep));
                builder.AddLine(center + Polar(outer, a + Sweep + Thickness));
                builder.AddQuadraticBezier(center + Polar((inner + outer) * 0.5f, a + Sweep * 0.22f + Thickness * 1.4f), center + Polar(inner, a + Thickness));
                builder.EndFigure(CanvasFigureLoop.Closed);
            }
            return CanvasGeometry.CreatePath(builder);
        }
    }
}
