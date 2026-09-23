using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace OpenSense.App.Controls;

/// <summary>
/// Lays visible children out in equal columns that always span the full width. It picks the column
/// count the children fill (four tiles become 4 or 2 × 2, never 3 + 1; two fans take half each),
/// and stretches every child in a row to that row's tallest.
/// </summary>
public sealed partial class TileGrid : Panel
{
    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(
        nameof(MinColumnWidth), typeof(double), typeof(TileGrid), new PropertyMetadata(220.0, (d, _) => ((TileGrid)d).InvalidateMeasure()));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(TileGrid), new PropertyMetadata(4, (d, _) => ((TileGrid)d).InvalidateMeasure()));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(TileGrid), new PropertyMetadata(16.0, (d, _) => ((TileGrid)d).InvalidateMeasure()));

    public double MinColumnWidth
    {
        get => (double)GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Visible();
        if (visible.Count == 0)
            return default;

        var columns = Columns(availableSize.Width, visible.Count);
        var columnWidth = double.IsInfinity(availableSize.Width) ? MinColumnWidth : ColumnWidth(availableSize.Width, columns);
        foreach (var child in visible)
            child.Measure(new Size(columnWidth, double.PositiveInfinity));

        var height = RowHeights(visible, columns).Sum() + Spacing * (RowCount(visible.Count, columns) - 1);
        var width = double.IsInfinity(availableSize.Width) ? columns * columnWidth + (columns - 1) * Spacing : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Visible();
        if (visible.Count == 0)
            return finalSize;

        var columns = Columns(finalSize.Width, visible.Count);
        var columnWidth = ColumnWidth(finalSize.Width, columns);
        var rows = RowHeights(visible, columns);
        var y = 0.0;
        for (var i = 0; i < visible.Count; i++)
        {
            var (row, column) = Math.DivRem(i, columns);
            if (column == 0 && row > 0)
                y += rows[row - 1] + Spacing;
            visible[i].Arrange(new Rect(column * (columnWidth + Spacing), y, columnWidth, rows[row]));
        }
        return finalSize;
    }

    private List<UIElement> Visible() => Children.Where(c => c.Visibility == Visibility.Visible).ToList();

    /// <summary>As many columns as fit, reduced until the last row wastes the fewest cells.</summary>
    private int Columns(double width, int count)
    {
        var fit = double.IsInfinity(width) ? count : (int)Math.Floor((width + Spacing) / (MinColumnWidth + Spacing));
        var most = Math.Clamp(Math.Min(fit, MaxColumns), 1, count);
        var best = most;
        for (var columns = most; columns >= 1; columns--)
        {
            if (EmptyCells(count, columns) < EmptyCells(count, best))
                best = columns;
        }
        return best;
    }

    private static int EmptyCells(int count, int columns) => RowCount(count, columns) * columns - count;

    private static int RowCount(int count, int columns) => (count + columns - 1) / columns;

    private double ColumnWidth(double width, int columns) => Math.Max(0, (width - Spacing * (columns - 1)) / columns);

    private static double[] RowHeights(List<UIElement> visible, int columns)
    {
        var rows = new double[RowCount(visible.Count, columns)];
        for (var i = 0; i < visible.Count; i++)
            rows[i / columns] = Math.Max(rows[i / columns], visible[i].DesiredSize.Height);
        return rows;
    }
}
