using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace GraniteEdgeAI.Features.ModelInspection.Controls;

/// <summary>
/// Arranges a vertical list so every row uses the tallest row's natural height.
/// </summary>
public sealed class EqualHeightStackPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double rowWidth = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0d, availableSize.Width);
        double rowHeight = 0d;
        double desiredWidth = 0d;

        foreach (UIElement child in Children)
        {
            child.Measure(new Size(rowWidth, double.PositiveInfinity));
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        return new Size(desiredWidth, rowHeight * Children.Count);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }

        double rowHeight = 0d;
        foreach (UIElement child in Children)
        {
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        double y = 0d;
        foreach (UIElement child in Children)
        {
            child.Arrange(new Rect(0d, y, finalSize.Width, rowHeight));
            y += rowHeight;
        }

        return new Size(finalSize.Width, y);
    }
}

/// <summary>
/// Provides the same equal-height vertical arrangement for an ItemsRepeater.
/// The inspection progress list is deliberately small, so all rows are measured
/// together to keep their visual rhythm and accessibility geometry consistent.
/// </summary>
public sealed class EqualHeightStackLayout : VirtualizingLayout
{
    protected override Size MeasureOverride(
        VirtualizingLayoutContext context,
        Size availableSize)
    {
        double rowWidth = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0d, availableSize.Width);
        double rowHeight = 0d;
        double desiredWidth = 0d;

        for (int index = 0; index < context.ItemCount; index++)
        {
            UIElement child = context.GetOrCreateElementAt(index);
            child.Measure(new Size(rowWidth, double.PositiveInfinity));
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        return new Size(desiredWidth, rowHeight * context.ItemCount);
    }

    protected override Size ArrangeOverride(
        VirtualizingLayoutContext context,
        Size finalSize)
    {
        double rowHeight = 0d;
        for (int index = 0; index < context.ItemCount; index++)
        {
            rowHeight = Math.Max(
                rowHeight,
                context.GetOrCreateElementAt(index).DesiredSize.Height);
        }

        double y = 0d;
        for (int index = 0; index < context.ItemCount; index++)
        {
            UIElement child = context.GetOrCreateElementAt(index);
            child.Arrange(new Rect(0d, y, finalSize.Width, rowHeight));
            y += rowHeight;
        }

        return new Size(finalSize.Width, y);
    }
}
