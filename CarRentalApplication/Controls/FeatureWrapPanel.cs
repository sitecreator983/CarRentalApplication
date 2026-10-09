using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CarRentalApplication.Controls;

// Measure and arrange with the same row boundaries so resizing also updates height.
public sealed class FeatureWrapPanel : Panel
{
    private const double Spacing = 10;

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));

        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        return finalSize;
    }

    private Size Layout(double availableWidth, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, width = 0;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            var childWidth = Math.Min(size.Width, availableWidth);
            if (x > 0 && x + childWidth > availableWidth)
            {
                y += rowHeight + Spacing;
                x = 0;
                rowHeight = 0;
            }

            if (arrange)
                child.Arrange(new Rect(x, y, childWidth, size.Height));
            width = Math.Max(width, x + childWidth);
            rowHeight = Math.Max(rowHeight, size.Height);
            x += childWidth + Spacing;
        }

        return new Size(width, y + rowHeight);
    }
}
