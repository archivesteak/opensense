namespace OpenSense.Core.Control;

/// <summary>
/// Turns a fan's curve-step temperatures into a steady boost. It follows the higher of the last two readings,
/// so the fans speed up at once but only slow down when the next reading confirms the drop (95 then 89 holds
/// 95's boost; a following 88 moves to 89's). Changes under <see cref="MinChangePercent"/> are not sent.
/// </summary>
public sealed class CurveFollower
{
    private const int MinChangePercent = 2;

    private double? _previous;

    public int? Current { get; private set; }

    public void Reset()
    {
        _previous = null;
        Current = null;
    }

    public int Update(double temperature, FanCurve curve)
    {
        var effective = _previous is { } previous ? Math.Max(previous, temperature) : temperature;
        _previous = temperature;

        var target = Math.Clamp(curve.Evaluate(effective), 0, 100);
        if (Current is { } current && Math.Abs(target - current) < MinChangePercent && target is not (0 or 100))
            target = current;

        Current = target;
        return target;
    }
}
