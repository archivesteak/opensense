namespace OpenSense.Core.Control;

/// <summary>
/// Turns noisy temperature readings into a steady fan duty: fast rise, slow fall, a hysteresis band
/// before slowing down, and a dead band so tiny changes are not sent to the firmware.
/// </summary>
public sealed class CurveFollower
{
    private double? _smoothed;

    public int? Current { get; private set; }

    public void Reset()
    {
        _smoothed = null;
        Current = null;
    }

    public int Update(double temperature, FanCurve curve, ResponseSettings response, int minimumPercent)
    {
        _smoothed = _smoothed is not { } s
            ? temperature
            : s + (temperature - s) * (temperature > s ? response.RiseSmoothing : response.FallSmoothing);

        var rising = curve.Evaluate(_smoothed.Value);
        int target;
        if (Current is not { } current || rising >= current)
        {
            target = rising;
        }
        else
        {
            // Only slow down once the temperature is HysteresisC below the point that justified the current speed.
            target = Math.Min(current, curve.Evaluate(_smoothed.Value + response.HysteresisC));
        }

        target = Math.Clamp(Math.Max(target, minimumPercent), 0, 100);
        if (Current is { } c && Math.Abs(target - c) < response.MinChangePercent && target is not (0 or 100) && target != minimumPercent)
            target = c;

        Current = target;
        return target;
    }
}
