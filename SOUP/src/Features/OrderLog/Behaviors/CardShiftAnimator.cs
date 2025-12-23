using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Behaviors;

/// <summary>
/// Manages card shift animations for fluid drag and drop.
/// Calculates insertion points and animates cards to make space for dragged items.
/// </summary>
public class CardShiftAnimator
{
    private readonly Panel _panel;
    private readonly Dictionary<FrameworkElement, TranslateTransform> _transforms = new();
    private readonly TimeSpan _animationDuration;
    private readonly IEasingFunction _easingFunction;

    public CardShiftAnimator(Panel panel, TimeSpan animationDuration)
    {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _animationDuration = animationDuration;
        _easingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    }

    /// <summary>
    /// Calculates the insertion index based on mouse position relative to the panel.
    /// </summary>
    public int CalculateInsertionIndex(Point mousePosition, FrameworkElement? draggedElement, out double avgItemSize)
    {
        // Use ALL children to maintain consistent indices with AnimateCardShift
        var children = GetVisibleChildren(null);
        if (children.Count == 0)
        {
            avgItemSize = 0;
            return 0;
        }

        if (_panel is StackPanel stackPanel)
        {
            return CalculateInsertionIndexStackPanel(mousePosition, children, draggedElement, out avgItemSize);
        }
        else if (_panel is WrapPanel)
        {
            return CalculateInsertionIndexWrapPanel(mousePosition, children, draggedElement, out avgItemSize);
        }

        avgItemSize = 0;
        return 0;
    }

    /// <summary>
    /// Animates cards to shift and make space for the dragged item.
    /// </summary>
    public void AnimateCardShift(int insertionIndex, int draggedIndex, FrameworkElement? draggedElement)
    {
        // Get ALL children including dragged to maintain correct indices
        var allChildren = GetVisibleChildren(null);
        if (allChildren.Count == 0) return;

        // Detect orientation for both StackPanel and WrapPanel
        var isVertical = (_panel is StackPanel sp && sp.Orientation == Orientation.Vertical) ||
                         (_panel is System.Windows.Controls.WrapPanel wp && wp.Orientation == Orientation.Vertical);

        // If this is a horizontal WrapPanel, but the dragged element (or any card) is wide
        // (close to panel width), prefer vertical shifting so full-width/merged cards move up/down.
        if (!isVertical && _panel is System.Windows.Controls.WrapPanel wrap)
        {
            double panelWidth = _panel.ActualWidth > 0 ? _panel.ActualWidth : wrap.ActualWidth;
            // Heuristic: treat as wide if element width is > 60% of panel width
            bool draggedIsWide = draggedElement != null && draggedElement.ActualWidth > panelWidth * 0.6;

            bool anyWide = allChildren.Any(c => c.ActualWidth > panelWidth * 0.6);

            if (draggedIsWide || anyWide)
            {
                isVertical = true;
            }
        }

        var avgSize = CalculateAverageCardSize(allChildren, isVertical);

        // Iterate through all children with their actual panel indices
        for (int i = 0; i < allChildren.Count; i++)
        {
            var child = allChildren[i];
            if (child == draggedElement) continue; // Skip the dragged element

            // Calculate shift using the actual panel index
            double targetOffset = CalculateShiftOffset(i, insertionIndex, draggedIndex, avgSize);
            AnimateCardToOffset(child, targetOffset, isVertical);
        }
    }

    /// <summary>
    /// Resets all card positions to their original state (no offset).
    /// </summary>
    public void ResetAllCardPositions()
    {
        // Detect orientation for both StackPanel and WrapPanel
        var isVertical = (_panel is StackPanel sp && sp.Orientation == Orientation.Vertical) ||
                         (_panel is System.Windows.Controls.WrapPanel wp && wp.Orientation == Orientation.Vertical);

        foreach (var (element, transform) in _transforms.ToList())
        {
            AnimateCardToOffset(element, 0, isVertical);
        }
    }

    /// <summary>
    /// Clears all transforms and removes them from elements.
    /// </summary>
    public void ClearTransforms()
    {
        foreach (var (element, _) in _transforms.ToList())
        {
            element.RenderTransform = null;
        }
        _transforms.Clear();
    }

    private List<FrameworkElement> GetVisibleChildren(FrameworkElement? excludeElement)
    {
        var result = new List<FrameworkElement>();

        // For ItemsControl panels, we need to find the actual card Border elements
        // They're nested inside ContentControl containers
        foreach (var panelChild in _panel.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            // Look for Border in the visual tree
            var border = FindBorderInElement(panelChild);
            if (border != null && border != excludeElement)
            {
                result.Add(border);
            }
        }

        return result;
    }

    private static FrameworkElement? FindBorderInElement(DependencyObject element)
    {
        // If this is a Border with OrderItem/OrderItemGroup data, return it
        if (element is Border border &&
            (border.DataContext is Models.OrderItem || border.DataContext is ViewModels.OrderItemGroup))
        {
            return border;
        }

        // Search children
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var result = FindBorderInElement(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private int CalculateInsertionIndexStackPanel(
        Point mousePosition,
        List<FrameworkElement> children,
        FrameworkElement? draggedElement,
        out double avgItemHeight)
    {
        avgItemHeight = CalculateAverageCardSize(children, isVertical: true);
        if (avgItemHeight == 0 || children.Count == 0)
        {
            return 0;
        }

        // Get the panel's starting Y position (use first non-dragged child's original position)
        double startY = 0;
        if (children.Count > 0)
        {
            var firstPos = children[0].TransformToAncestor(_panel).Transform(new Point(0, 0));
            if (_transforms.TryGetValue(children[0], out var firstTransform))
            {
                startY = firstPos.Y - firstTransform.Y;
            }
            else
            {
                startY = firstPos.Y;
            }
        }

        // Calculate cumulative positions for each child in their ORIGINAL layout (as if dragged element is removed)
        double cumulativeY = startY;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];

            // Skip the dragged element - it's being repositioned
            if (child == draggedElement)
            {
                continue;
            }

            var childHeight = child.ActualHeight > 0 ? child.ActualHeight : avgItemHeight;
            var childMidpoint = cumulativeY + (childHeight / 2);

            if (mousePosition.Y < childMidpoint)
            {
                return i;
            }

            // Advance to next position (include margin)
            cumulativeY += childHeight;
            if (child.Margin.Bottom > 0)
            {
                cumulativeY += child.Margin.Bottom;
            }
        }

        return children.Count;
    }

    private int CalculateInsertionIndexWrapPanel(
        Point mousePosition,
        List<FrameworkElement> children,
        FrameworkElement? draggedElement,
        out double avgItemSize)
    {
        avgItemSize = 0;
        if (children.Count == 0) return 0;

        // Prefer vertical placement for WrapPanel: find the first child whose vertical midpoint
        // is below the mouse Y. This makes wide (full-width) cards move up/down rather than
        // shifting sideways into the other column.
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == draggedElement) continue;

            var childPos = child.TransformToAncestor(_panel).Transform(new Point(0, 0));
            // Subtract any transform offset to get original layout position
            if (_transforms.TryGetValue(child, out var transform))
            {
                childPos.X -= transform.X;
                childPos.Y -= transform.Y;
            }

            var childMidY = childPos.Y + (child.ActualHeight / 2);

            if (mousePosition.Y < childMidY)
            {
                avgItemSize = children.FirstOrDefault()?.ActualWidth ?? 0;
                return i;
            }
        }

        // If mouse is below all items, insert at end
        avgItemSize = children.FirstOrDefault()?.ActualWidth ?? 0;
        return children.Count;
    }

    private double CalculateAverageCardSize(List<FrameworkElement> children, bool isVertical)
    {
        if (children.Count == 0) return 0;

        var sizes = children
            .Select(c => isVertical ? c.ActualHeight : c.ActualWidth)
            .Where(size => size > 0)
            .ToList();

        if (sizes.Count == 0)
        {
            // Fallback to DesiredSize if ActualSize not available
            sizes = children
                .Select(c => isVertical ? c.DesiredSize.Height : c.DesiredSize.Width)
                .Where(size => size > 0)
                .ToList();
        }

        return sizes.Count > 0 ? sizes.Average() : 0;
    }

    private double CalculateShiftOffset(int cardIndex, int insertionIndex, int draggedIndex, double cardSize)
    {
        // Visual approach: shift cards to make space at the insertion point
        // Cards at/after insertion point shift down/forward to make room

        if (insertionIndex < draggedIndex)
        {
            // Dragging backward: cards between insertion and dragged position shift down
            if (cardIndex >= insertionIndex && cardIndex < draggedIndex)
            {
                return cardSize; // Shift down/forward
            }
        }
        else if (insertionIndex > draggedIndex)
        {
            // Dragging forward: cards between dragged and insertion shift down
            if (cardIndex > draggedIndex && cardIndex < insertionIndex)
            {
                return cardSize; // Shift down/forward to make space
            }
        }

        return 0; // No shift
    }

    private void AnimateCardToOffset(FrameworkElement element, double targetOffset, bool isVertical)
    {
        var transform = GetOrCreateTransform(element);

        var animation = new DoubleAnimation
        {
            To = targetOffset,
            Duration = _animationDuration,
            EasingFunction = _easingFunction
        };

        var property = isVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty;
        transform.BeginAnimation(property, animation);
    }

    private TranslateTransform GetOrCreateTransform(FrameworkElement element)
    {
        if (_transforms.TryGetValue(element, out var existingTransform))
        {
            return existingTransform;
        }

        var transform = new TranslateTransform();

        // Check if element already has a RenderTransform
        if (element.RenderTransform is TransformGroup existingGroup)
        {
            existingGroup.Children.Add(transform);
        }
        else if (element.RenderTransform != null && element.RenderTransform != Transform.Identity)
        {
            // Wrap existing transform in a group
            var group = new TransformGroup();
            group.Children.Add(element.RenderTransform);
            group.Children.Add(transform);
            element.RenderTransform = group;
        }
        else
        {
            element.RenderTransform = transform;
        }

        _transforms[element] = transform;
        return transform;
    }
}
