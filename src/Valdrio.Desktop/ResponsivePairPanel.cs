using System.Windows;
using System.Windows.Controls;

namespace Valdrio.Desktop;

// Two related work areas share wide viewports and stack in reading order on narrow ones.
// Natural height lets the enclosing page own vertical scrolling.
public sealed class ResponsivePairPanel : Panel
{
    private const double Gap = 12;
    private const double WideWidth = 1000;
    protected override Size MeasureOverride(Size availableSize)
    {
        var wide = double.IsFinite(availableSize.Width) && availableSize.Width >= WideWidth;
        var width = wide ? (availableSize.Width - Gap) / 2 : availableSize.Width;
        double height = 0, desiredWidth = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            height = wide ? Math.Max(height, child.DesiredSize.Height) : height + child.DesiredSize.Height;
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
        }
        if (!wide && InternalChildren.Count > 1) height += Gap * (InternalChildren.Count - 1);
        return new Size(wide ? desiredWidth * 2 + Gap : desiredWidth, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var wide = finalSize.Width >= WideWidth;
        var width = wide ? (finalSize.Width - Gap) / 2 : finalSize.Width;
        double offset = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(wide
                ? new Rect(offset, 0, width, child.DesiredSize.Height)
                : new Rect(0, offset, width, child.DesiredSize.Height));
            offset += (wide ? width : child.DesiredSize.Height) + Gap;
        }
        return finalSize;
    }
}
