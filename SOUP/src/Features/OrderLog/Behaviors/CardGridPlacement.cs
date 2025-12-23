using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SOUP.Features.OrderLog.Behaviors;

/// <summary>
/// Helper that maps card visuals into a simple 2-column grid and computes
/// an insertion index based on mouse position. Supports single- and
/// double-wide cards (double-wide occupy both columns in a row).
///
/// This is intentionally conservative: it inspects ActualWidth/Height
/// of card Borders and the panel width to determine spans.
/// </summary>
internal static class CardGridPlacement
{
    public static int CalculateInsertionIndexGrid(Panel panel, Rect draggedRect, FrameworkElement? draggedElement, double? lockedPanelWidth = null)
    {
        if (panel == null) return 0;

        // Build list of card Border elements in panel order (including dragged element)
        var children = new List<FrameworkElement>();
        foreach (var panelChild in panel.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;
            var border = FindVisualChildOfType<Border>(panelChild);
            if (border != null && (border.DataContext != null))
            {
                children.Add(border);
            }
        }

        if (children.Count == 0) return 0;

        double panelWidth = Math.Max(1, lockedPanelWidth ?? panel.ActualWidth);

        // Estimate average child width to determine number of columns
        var measuredWidths = children.Select(c => c.ActualWidth).Where(w => w > 0).ToList();
        double avgChildWidth = measuredWidths.Count > 0 ? measuredWidths.Average() : panelWidth / 2.0;

        int columns = Math.Max(1, (int)Math.Floor(panelWidth / Math.Max(1, avgChildWidth)));
        double colWidth = panelWidth / (double)columns;

        // Decide span: treat a card as full-width (span==columns) if its ActualWidth is near full panel width
        int GetSpanLocal(FrameworkElement e)
        {
            if (e == null || e.ActualWidth <= 0) return 1;
            return e.ActualWidth >= panelWidth * 0.75 ? columns : 1;
        }

        // Place non-dragged children into slot map (slot index -> child index)
        // Slot numbering: row*columns + col
        var slotToChild = new Dictionary<int, int>();
        int nextSlot = 0;
        var placementChildren = new List<FrameworkElement>();
        var placementOriginalIndex = new List<int>();
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == draggedElement) continue; // skip dragged when laying out
            placementOriginalIndex.Add(i);
            placementChildren.Add(child);

            var span = GetSpanLocal(child);

            // If span > 1 and we're not at column 0, move to next row
            if (span > 1 && (nextSlot % columns) != 0)
            {
                nextSlot += (columns - (nextSlot % columns));
            }

            // assign slots
            for (int s = 0; s < span; s++)
            {
                slotToChild[nextSlot + s] = placementChildren.Count - 1; // index into placementChildren
            }
            nextSlot += span;
        }

        // Compute row heights by inspecting actual children occupying that row
        int totalRows = Math.Max(1, (int)Math.Ceiling(nextSlot / (double)columns));
        var rowHeights = new double[totalRows];
        for (int slot = 0; slot < nextSlot; slot++)
        {
            int row = slot / columns;
            if (slotToChild.TryGetValue(slot, out var childIndex))
            {
                var child = children[childIndex];
                var h = (child.ActualHeight > 0 ? child.ActualHeight : 40) + child.Margin.Top + child.Margin.Bottom;
                rowHeights[row] = Math.Max(rowHeights[row], h);
            }
        }

        // If no heights discovered, fallback to average height
        if (rowHeights.All(h => h == 0))
        {
            var avg = children.Select(c => (c.ActualHeight > 0 ? c.ActualHeight : 40) + c.Margin.Top + c.Margin.Bottom).DefaultIfEmpty(40).Average();
            for (int r = 0; r < rowHeights.Length; r++) rowHeights[r] = avg;
        }

        // Compute candidate slot centers and choose the slot whose center is nearest to
        // the dragged rectangle center. This bases decisions on card geometry instead of
        // the mouse pointer.
        var rowY = new List<double>();
        double cum = 0;
        for (int r = 0; r < rowHeights.Length; r++)
        {
            rowY.Add(cum);
            cum += rowHeights[r];
        }

        // Center of dragged rect
        var dragCenter = new Point(draggedRect.X + draggedRect.Width / 2.0, draggedRect.Y + draggedRect.Height / 2.0);

        // Determine dragged span
        int draggedSpan = draggedElement != null ? GetSpan(panel, draggedElement, lockedPanelWidth) : 1;
        draggedSpan = Math.Min(draggedSpan, Math.Max(1, columns));

        // Consider candidate start slots from 0..nextSlot (allow append at end)
        int bestSlot = nextSlot;
        double bestDist = double.MaxValue;

        for (int s = 0; s <= nextSlot; s++)
        {
            // Align multi-column start to row boundary if needed
            int aligned = s;
            if (draggedSpan > 1 && (aligned % columns) != 0)
                aligned += (columns - (aligned % columns));

            int row = aligned / columns;
            int col = aligned % columns;

            double slotX = col * colWidth;
            double slotY = row < rowY.Count ? rowY[row] : cum;
            double slotW = colWidth * draggedSpan;
            double slotH = row < rowHeights.Length ? rowHeights[row] : 40;

            var slotCenter = new Point(slotX + slotW / 2.0, slotY + slotH / 2.0);
            double dx = slotCenter.X - dragCenter.X;
            double dy = slotCenter.Y - dragCenter.Y;
            double dist = dx * dx + dy * dy; // squared distance

            if (dist < bestDist)
            {
                bestDist = dist;
                bestSlot = aligned;
            }
        }

        int desiredSlot = bestSlot;

        // Find insertion index as the first child whose assigned slot >= desiredSlot
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == draggedElement)
            {
                // If we reach the dragged element in the original order, insert here
                return i;
            }

            // Find this child's starting slot in placementChildren mapping
            int placementIdx = placementOriginalIndex.IndexOf(i);
            int startSlot = int.MaxValue;
            if (placementIdx >= 0)
            {
                var slotsForChild = slotToChild.Where(kv => kv.Value == placementIdx).Select(kv => kv.Key).ToList();
                startSlot = slotsForChild.Count > 0 ? slotsForChild.Min() : int.MaxValue;
            }

            if (startSlot >= desiredSlot)
            {
                return i;
            }
        }

        // Append at end
        return children.Count;
    }

    public static bool IsDoubleWideCard(Panel panel, FrameworkElement? element, double? lockedPanelWidth = null)
    {
        if (element == null) return false;
        if (panel == null) return false;
        var panelWidth = Math.Max(1, lockedPanelWidth ?? panel.ActualWidth);
        if (element.ActualWidth <= 0) return false;
        return element.ActualWidth >= panelWidth * 0.75;
    }

    public static int GetSpan(Panel panel, FrameworkElement? element, double? lockedPanelWidth = null)
    {
        if (panel == null || element == null) return 1;
        var panelWidth = Math.Max(1, lockedPanelWidth ?? panel.ActualWidth);
        return element.ActualWidth >= panelWidth * 0.75 ? Math.Max(1, (int)Math.Floor(panelWidth / Math.Max(1, element.ActualWidth))) : 1;
    }

    public static void ComputeSlotLayout(Panel panel, List<FrameworkElement> children, out Dictionary<int,int> slotToChild, out int nextSlot, out double colWidth, out double[] rowHeights, double? lockedPanelWidth = null)
    {
        slotToChild = new Dictionary<int, int>();
        nextSlot = 0;
        double panelWidth = Math.Max(1, lockedPanelWidth ?? panel.ActualWidth);

        // Compute average child width and average horizontal spacing (margins)
        var measuredWidths = children.Select(c => c.ActualWidth).Where(w => w > 0).ToList();
        double avgChildWidth = measuredWidths.Count > 0 ? measuredWidths.Average() : panelWidth / 2.0;
        var measuredHMargins = children.Select(c => c.Margin.Left + c.Margin.Right).Where(m => m >= 0).ToList();
        double avgHMargin = measuredHMargins.Count > 0 ? measuredHMargins.Average() : 16.0; // fallback margin

        // Try to derive actual column X positions from each child's rendered left coordinate.
        // This makes slot X positions align with visuals even when margins or varying widths exist.
        List<double> leftPositions = new();
        foreach (var c in children)
        {
            try
            {
                var p = c.TransformToAncestor(panel).Transform(new Point(0, 0));
                double left = p.X;

                // If element has a TranslateTransform applied (via RenderTransform), subtract it to get original layout X
                double tx = 0;
                if (c.RenderTransform is TranslateTransform t)
                {
                    tx = t.X;
                }
                else if (c.RenderTransform is TransformGroup g)
                {
                    foreach (var ch in g.Children)
                    {
                        if (ch is TranslateTransform tt) { tx += tt.X; }
                    }
                }
                left -= tx;
                leftPositions.Add(left);
            }
            catch
            {
                // ignore transform errors and skip
            }
        }

        // Group left positions into column starts using a small tolerance
        var uniqueLefts = new List<double>();
        const double tol = 8.0;
        foreach (var l in leftPositions.OrderBy(x => x))
        {
            if (uniqueLefts.Count == 0) uniqueLefts.Add(l);
            else if (Math.Abs(l - uniqueLefts.Last()) > tol) uniqueLefts.Add(l);
            else
            {
                // merge close positions by averaging
                var last = uniqueLefts.Last();
                uniqueLefts[uniqueLefts.Count - 1] = (last + l) / 2.0;
            }
        }

        int columns = Math.Max(1, uniqueLefts.Count);

        if (uniqueLefts.Count > 1)
        {
            // compute average stride between column starts
            var diffs = new List<double>();
            for (int i = 1; i < uniqueLefts.Count; i++) diffs.Add(uniqueLefts[i] - uniqueLefts[i - 1]);
            colWidth = diffs.Count > 0 ? diffs.Average() : (avgChildWidth + avgHMargin);
        }
        else
        {
            // fallback to estimated stride
            colWidth = avgChildWidth + avgHMargin;
        }

        if (children == null || children.Count == 0)
        {
            rowHeights = Array.Empty<double>();
            return;
        }

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var span = GetSpan(panel, child, lockedPanelWidth);

            if (span > 1 && (nextSlot % columns) != 0)
                nextSlot += (columns - (nextSlot % columns));

            slotToChild[nextSlot] = i;
            for (int s = 1; s < span; s++)
            {
                slotToChild[nextSlot + s] = i;
            }
            nextSlot += span;
        }

        int totalRows = Math.Max(1, (int)Math.Ceiling(nextSlot / (double)columns));
        rowHeights = new double[totalRows];
        for (int slot = 0; slot < nextSlot; slot++)
        {
            int row = slot / columns;
            if (slotToChild.TryGetValue(slot, out var childIndex))
            {
                var child = children[childIndex];
                var h = (child.ActualHeight > 0 ? child.ActualHeight : 40) + child.Margin.Top + child.Margin.Bottom;
                rowHeights[row] = Math.Max(rowHeights[row], h);
            }
        }

        if (rowHeights.All(h => h == 0))
        {
            var avg = children.Select(c => c.ActualHeight).Where(h => h > 0).DefaultIfEmpty(40).Average();
            for (int r = 0; r < rowHeights.Length; r++) rowHeights[r] = avg;
        }
    }

    private static T? FindVisualChildOfType<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return default;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var res = FindVisualChildOfType<T>(child);
            if (res != null) return res;
        }
        return default;
    }
}
