using OpenSense.App.Helpers;

namespace OpenSense.Core.Tests;

public class CurveLabelTests
{
    [Theory]
    [InlineData(80, 60, 10, 120, 50, 60)]
    [InlineData(80, 200, 10, 40, 10, 40)]
    [InlineData(80, 200, 10, 0, 10, 0)]
    [InlineData(-100, 50, 10, 120, 10, 50)]
    [InlineData(1000, 50, 10, 120, 80, 50)]
    public void Labels_fit_narrow_and_zero_width_plots_without_invalid_clamp_bounds(
        float anchor, float labelWidth, float plotLeft, float plotWidth, float expectedLeft, float expectedWidth)
    {
        var placed = CurveLabelPlacement.Fit(anchor, labelWidth, plotLeft, plotWidth);
        Assert.Equal(expectedLeft, placed.Left);
        Assert.Equal(expectedWidth, placed.Width);
    }
}
