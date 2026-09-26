using OpenSense.Core.Hardware;
using OpenSense.Core.Monitoring;

namespace OpenSense.Core.Control;

/// <summary>
/// Auto's boost (Anti-throttle), and Custom's Default preset: each fan is left to the firmware until its chip nears
/// the temperature where it would slow itself down (<see cref="ThermalLimits"/>), and reaches full speed by then.
/// Below that the chips don't slow down, so a boost there would only add noise.
/// </summary>
public static class AntiThrottle
{
    /// <summary>
    /// The CPU fan's curve: from 12 °C under the CPU's limit to full speed 6 °C under it. Intel's Dynamic Tuning, as
    /// Acer sets it up on the AN515-57, starts cutting the CPU's power at 94 °C (TjMax 100 °C), and the package
    /// temperature peaks a few degrees over the average the curve steps on.
    /// </summary>
    public static FanCurve Cpu(int limit) => FanCurve.From((limit - 12, 0), (limit - 6, 100));

    /// <summary>
    /// A GPU fan's curve: from 6 °C under the GPU's limit to full speed at it. The driver holds the GPU there by lowering
    /// its clocks bit by bit, and the GPU's temperature moves slowly.
    /// </summary>
    public static FanCurve Gpu(int limit) => FanCurve.From((limit - 6, 0), (limit, 100));

    /// <summary>The curve of a fan that cools <paramref name="chip"/>.</summary>
    public static FanCurve For(FanChip chip, ThermalLimits limits) => chip == FanChip.Gpu ? Gpu(limits.Gpu) : Cpu(limits.Cpu);
}
