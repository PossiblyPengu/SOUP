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
    public void AnimateCardShift(int insertionIndex, int draggedIndex, FrameworkElement? draggedElement, double? lockedPanelWidth = null)
    {
        // Get ALL children including dragged to maintain correct indices
        var allChildren = GetVisibleChildren(null);
        if (allChildren.Count == 0) return;

        // Detect orientation for both StackPanel and WrapPanel
        var isVertical = (_panel is StackPanel sp && sp.Orientation == Orientation.Vertical) ||
                         (_panel is System.Windows.Controls.WrapPanel wp && wp.Orientation == Orientation.Vertical);

        // For WrapPanel, check if we're swapping within the same row (horizontal swap)
        // or between rows (vertical shift)
        if (_panel is System.Windows.Controls.WrapPanel && draggedElement != null)
        {
            // Get ORIGINAL positions (without drag transforms) to detect row placement
            var draggedPos = draggedElement.TransformToAncestor(_panel).Transform(new Point(0, 0));
            if (_transforms.TryGetValue(draggedElement, out var draggedTransform))
            {
                draggedPos.Y -= draggedTransform.Y; // Remove drag offset to get original position
            }
            var draggedY = draggedPos.Y;

            // Find the target card at the insertion index
            FrameworkElement? targetCard = null;
            int adjustedInsertionIndex = insertionIndex > draggedIndex ? insertionIndex - 1 : insertionIndex;
            if (adjustedInsertionIndex >= 0 && adjustedInsertionIndex < allChildren.Count)
            {
                var candidateIndex = 0;
                foreach (var child in allChildren)
                {
                    if (child != draggedElement)
                    {
                        if (candidateIndex == adjustedInsertionIndex)
                        {
                            targetCard = child;
                            break;
                        }
                        candidateIndex++;
                    }
                }
            }

            if (targetCard != null)
            {
                // Get target's ORIGINAL position (without shift transforms)
                var targetPos = targetCard.TransformToAncestor(_panel).Transform(new Point(0, 0));
                if (_transforms.TryGetValue(targetCard, out var targetTransform))
                {
                    targetPos.Y -= targetTransform.Y; // Remove shift offset to get original position
                }
                var targetY = targetPos.Y;

                // If cards are in the same row (similar Y position), use horizontal shifting
                // Allow for some tolerance (20 pixels) for cards in the same row
                if (Math.Abs(draggedY - targetY) < 20)
                {
                    isVertical = false; // Use horizontal shifting for same-row swaps
                }
                else
                {
                    isVertical = true; // Use vertical shifting for cross-row moves
                }
            }
        }

        // Use the dragged element's actual size (not average) to prevent overlaps
        double draggedSize = 0;
        if (draggedElement != null)
        {
            draggedSize = isVertical ? draggedElement.ActualHeight : draggedElement.ActualWidth;

            // Include BOTH margins to get the total space occupied by the card
            if (isVertical)
            {
                draggedSize += draggedElement.Margin.Top + draggedElement.Margin.Bottom;
            }
            else
            {
                draggedSize += draggedElement.Margin.Left + draggedElement.Margin.Right;
            }
        }

        // Fallback to average size if dragged element size is not available
        if (draggedSize == 0)
        {
            draggedSize = CalculateAverageCardSize(allChildren, isVertical);
        }

        // If this is a WrapPanel, perform grid-aware slot shifting (2 columns)
        if (_panel is System.Windows.Controls.WrapPanel && draggedElement != null)
        {
            // Build placement list (exclude dragged)
            var placementChildren = allChildren.Where(c => c != draggedElement).ToList();

            // Compute slot layout for the placement children (respect locked panel width)
            CardGridPlacement.ComputeSlotLayout(_panel, placementChildren, out var slotToChild, out var nextSlot, out var colWidth, out var rowHeights, lockedPanelWidth);

            // Map child index -> startSlot (minimum slot assigned to that child)
            var childStartSlot = new Dictionary<int, int>();
            foreach (var kv in slotToChild)
            {
                var slot = kv.Key;
                var childIdx = kv.Value;
                if (!childStartSlot.ContainsKey(childIdx) || slot < childStartSlot[childIdx])
                    childStartSlot[childIdx] = slot;
            }

            // Determine dragged span (1 or full-width columns)
            // Derive columns from computed colWidth
            int columns = Math.Max(1, (int)Math.Round((lockedPanelWidth ?? _panel.ActualWidth) / colWidth));
            int draggedSpan = CardGridPlacement.GetSpan(_panel, draggedElement, lockedPanelWidth);

            // Convert insertionIndex (which is relative to allChildren including the dragged element)
            // to an index into placementChildren (which excludes the dragged element).
            int adjustedPlacementIndex = insertionIndex;
            if (insertionIndex > draggedIndex) adjustedPlacementIndex = insertionIndex - 1;

            // Determine desired slot from adjusted placement index
            int desiredSlot;
            if (adjustedPlacementIndex >= 0 && adjustedPlacementIndex < placementChildren.Count)
            {
                if (childStartSlot.TryGetValue(adjustedPlacementIndex, out var s))
                    desiredSlot = s;
                else
                    desiredSlot = nextSlot;
            }
            else
            {
                desiredSlot = nextSlot; // append at end
            }

            // Align desired slot for dragged elements that span multiple columns
            if (draggedSpan > 1 && (desiredSlot % columns) != 0)
                desiredSlot += (columns - (desiredSlot % columns));

            // Build ordered list of placement children by their start slot (unique)
            var orderedChildren = new List<int>(); // holds indices into placementChildren
            for (int slot = 0; slot < nextSlot; slot++)
            {
                if (slotToChild.TryGetValue(slot, out var childIdx))
                {
                    if (orderedChildren.Count == 0 || orderedChildren.Last() != childIdx)
                        orderedChildren.Add(childIdx);
                }
            }

            // Find insertion position in orderedChildren based on desiredSlot
            int insertionOrderIndex = orderedChildren.Count;
            for (int i = 0; i < orderedChildren.Count; i++)
            {
                var chIdx = orderedChildren[i];
                if (childStartSlot.TryGetValue(chIdx, out var startSlot))
                {
                    if (startSlot >= desiredSlot)
                    {
                        insertionOrderIndex = i;
                        break;
                    }
                }
            }

            // Build new order including placeholder for dragged element
            var newOrder = new List<int>(orderedChildren);
            newOrder.Insert(insertionOrderIndex, -1); // -1 = dragged placeholder

            // Assign new slots by walking the newOrder
            var newStartPerChild = new Dictionary<int, int>();
            int slotCursor = 0;
            foreach (var entry in newOrder)
            {
                    if (entry == -1)
                    {
                        // dragged placeholder
                        if (draggedSpan > 1 && (slotCursor % columns) != 0)
                            slotCursor += (columns - (slotCursor % columns));
                        slotCursor += draggedSpan;
                        continue;
                    }

                // Ensure multi-column children start at column 0 of a row
                int span = CardGridPlacement.GetSpan(_panel, placementChildren[entry]);
                if (span > 1 && (slotCursor % columns) != 0)
                    slotCursor += (columns - (slotCursor % columns));

                newStartPerChild[entry] = slotCursor;
                slotCursor += span;
            }

            // Compute cumulative row Y offsets
            var rowY = new List<double>();
            double cum = 0;
            for (int r = 0; r < rowHeights.Length; r++)
            {
                rowY.Add(cum);
                cum += rowHeights[r];
            }

            // Animate each placement child to its new X/Y
            foreach (var kv in newStartPerChild)
            {
                var idx = kv.Key;
                var child = placementChildren[idx];
                int newStart = kv.Value;

                int newRow = newStart / columns;
                int newCol = newStart % columns;

                double targetX = newCol * colWidth;
                double targetY = newRow < rowY.Count ? rowY[newRow] : cum;

                // Compute original logical position based on slot layout rather than
                // relying on the WrapPanel visual positions (which may wrap differently
                // at runtime). This prevents wrapping-sensitive offsets.
                if (!childStartSlot.TryGetValue(idx, out var oldStart))
                    continue;

                int oldRow = oldStart / columns;
                int oldCol = oldStart % columns;
                double origX = oldCol * colWidth;
                double origY = oldRow < rowY.Count ? rowY[oldRow] : cum;

                double offsetX = targetX - origX;
                double offsetY = targetY - origY;

                AnimateCardToOffsetXY(child, offsetX, offsetY);
            }

            return;
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

        // Use dragged element's center position instead of mouse position
        // Prefer the ORIGINAL layout Y (remove any live drag translate) to avoid
        // mis-detecting rows while the element is being dragged.
        double comparisonY = mousePosition.Y;
        if (draggedElement != null)
        {
            var draggedPos = draggedElement.TransformToAncestor(_panel).Transform(new Point(0, 0));
            if (_transforms.TryGetValue(draggedElement, out var dragTransform))
            {
                draggedPos.Y -= dragTransform.Y; // remove live drag offset
            }
            comparisonY = draggedPos.Y + (draggedElement.ActualHeight / 2);
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

            // Account for top margin in the current position
            var childTop = cumulativeY + child.Margin.Top;
            var childMidpoint = childTop + (childHeight / 2);

            if (comparisonY < childMidpoint)
            {
                return i;
            }

            // Advance to next position (include full height + both margins)
            cumulativeY += child.Margin.Top + childHeight + child.Margin.Bottom;
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

        // Use dragged element's center position instead of mouse position
        // Use the ORIGINAL layout Y (subtract any active translate) so the
        // insertion calculation doesn't flip to same-row logic mid-drag.
        double comparisonY = mousePosition.Y;
        if (draggedElement != null)
        {
            var draggedPos = draggedElement.TransformToAncestor(_panel).Transform(new Point(0, 0));
            if (_transforms.TryGetValue(draggedElement, out var dragTransform))
            {
                draggedPos.Y -= dragTransform.Y; // remove live drag offset
            }
            comparisonY = draggedPos.Y + (draggedElement.ActualHeight / 2);
        }

        // Prefer vertical placement for WrapPanel: find the first child whose vertical midpoint
        // is below the dragged card's center. This makes wide (full-width) cards move up/down rather than
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

            if (comparisonY < childMidY)
            {
                avgItemSize = children.FirstOrDefault()?.ActualWidth ?? 0;
                return i;
            }
        }

        // If dragged card is below all items, insert at end
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
        // Visual approach: cards move in the same direction as the drag to "make space"
        // This creates an intuitive visual where cards get out of the way

        if (insertionIndex < draggedIndex)
        {
            // Dragging backward (up): cards between insertion and dragged position shift up
            if (cardIndex >= insertionIndex && cardIndex < draggedIndex)
            {
                return -cardSize; // Shift UP to make space for the dragged item coming from below
            }
        }
        else if (insertionIndex > draggedIndex)
        {
            // Dragging forward (down): cards between dragged and insertion shift down
            if (cardIndex > draggedIndex && cardIndex < insertionIndex)
            {
                return cardSize; // Shift DOWN to make space for the dragged item coming from above
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

    private void AnimateCardToOffsetXY(FrameworkElement element, double offsetX, double offsetY)
    {
        var transform = GetOrCreateTransform(element);

        var animX = new DoubleAnimation
        {
            To = offsetX,
            Duration = _animationDuration,
            EasingFunction = _easingFunction
        };

        var animY = new DoubleAnimation
        {
            To = offsetY,
            Duration = _animationDuration,
            EasingFunction = _easingFunction
        };

        transform.BeginAnimation(TranslateTransform.XProperty, animX);
        transform.BeginAnimation(TranslateTransform.YProperty, animY);
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
