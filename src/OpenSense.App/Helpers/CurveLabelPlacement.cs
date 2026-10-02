namespace OpenSense.App.Helpers;

/// <summary>Fits a curve label inside its plot even when translated text is wider than the available space.</summary>
internal static class CurveLabelPlacement
{
    public static (float Left, float Width) Fit(float anchor, float measuredWidth, float plotLeft, float plotWidth)
    {
        var width = Math.Min(Math.Max(0, measuredWidth), Math.Max(0, plotWidth));
        return (Math.Clamp(anchor - width / 2, plotLeft, plotLeft + Math.Max(0, plotWidth - width)), width);
    }
}
