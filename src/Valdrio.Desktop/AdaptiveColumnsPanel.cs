using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

// Reflows the same controls in reading order; bindings and focusable elements are retained.
public sealed class AdaptiveColumnsPanel : Panel
{
    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(
        nameof(MinColumnWidth), typeof(double), typeof(AdaptiveColumnsPanel),
        new FrameworkPropertyMetadata(280d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty MinimumRowHeightProperty = DependencyProperty.Register(
        nameof(MinimumRowHeight), typeof(double), typeof(AdaptiveColumnsPanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double MinColumnWidth { get => (double)GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }
    public double MinimumRowHeight { get => (double)GetValue(MinimumRowHeightProperty); set => SetValue(MinimumRowHeightProperty, value); }
    private int Columns(double width) => double.IsPositiveInfinity(width) || width >= MinColumnWidth * 2 ? 2 : 1;
    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Columns(availableSize.Width);
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth : availableSize.Width / columns;
        var heights = new double[(InternalChildren.Count + columns - 1) / columns];
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(new Size(width, MinimumRowHeight > 0 ? MinimumRowHeight : double.PositiveInfinity));
            heights[i / columns] = Math.Max(heights[i / columns], Math.Max(MinimumRowHeight, child.DesiredSize.Height));
        }
        return new Size(width * columns, heights.Sum());
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns(finalSize.Width);
        var width = finalSize.Width / columns;
        var top = 0d;
        for (var row = 0; row * columns < InternalChildren.Count; row++)
        {
            var count = Math.Min(columns, InternalChildren.Count - row * columns);
            var height = MinimumRowHeight;
            for (var c = 0; c < count; c++) height = Math.Max(height, InternalChildren[row * columns + c].DesiredSize.Height);
            for (var c = 0; c < count; c++) InternalChildren[row * columns + c].Arrange(new Rect(c * width, top, width, height));
            top += height;
        }
        return finalSize;
    }
}
