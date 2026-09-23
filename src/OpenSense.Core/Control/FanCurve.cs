namespace OpenSense.Core.Control;

public sealed record CurvePoint(int Temperature, int Percent);

/// <summary>Temperature (°C) → fan duty (%) mapping, linear between points, flat beyond the ends.</summary>
public sealed record FanCurve
{
    public const int MinTemperature = 20;
    public const int MaxTemperature = 100;
    public const int MaxPoints = 12;

    private readonly IReadOnlyList<CurvePoint> _points = [];

    public IReadOnlyList<CurvePoint> Points
    {
        get => _points;
        init => _points = value
            .Select(p => new CurvePoint(Math.Clamp(p.Temperature, MinTemperature, MaxTemperature), Math.Clamp(p.Percent, 0, 100)))
            .OrderBy(p => p.Temperature)
            .ToList();
    }

    public int Evaluate(double temperature)
    {
        var pts = _points;
        if (pts.Count == 0)
            return 100;
        if (temperature <= pts[0].Temperature)
            return pts[0].Percent;
        for (var i = 1; i < pts.Count; i++)
        {
            var (a, b) = (pts[i - 1], pts[i]);
            if (temperature > b.Temperature)
                continue;
            if (b.Temperature == a.Temperature)
                return Math.Max(a.Percent, b.Percent);
            var t = (temperature - a.Temperature) / (b.Temperature - a.Temperature);
            return (int)Math.Round(a.Percent + (b.Percent - a.Percent) * t);
        }
        return pts[^1].Percent;
    }

    public static FanCurve From(params (int Temperature, int Percent)[] points) =>
        new() { Points = points.Select(p => new CurvePoint(p.Temperature, p.Percent)).ToList() };

    public bool Equals(FanCurve? other) => other is not null && _points.SequenceEqual(other._points);

    public override int GetHashCode() => _points.Aggregate(17, (h, p) => HashCode.Combine(h, p));
}

public static class CurvePresets
{
    public static FanCurve Silent { get; } = FanCurve.From((45, 0), (55, 20), (65, 35), (75, 55), (85, 80), (92, 100));
    public static FanCurve Balanced { get; } = FanCurve.From((40, 15), (50, 25), (60, 40), (70, 55), (80, 75), (88, 100));
    public static FanCurve Performance { get; } = FanCurve.From((40, 30), (50, 45), (60, 60), (70, 75), (78, 90), (85, 100));

    public static IReadOnlyList<(string Name, FanCurve Curve)> All { get; } =
        [("Silent", Silent), ("Balanced", Balanced), ("Performance", Performance)];
}
