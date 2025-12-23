using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using Microsoft.Xaml.Behaviors;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Behaviors;

/// <summary>
/// New grid-aware drag behavior. Uses card geometry (bounds) and a slot map
/// computed from measured card sizes to determine the nearest empty slot and
/// animates other cards to make room. Intended as a drop-in replacement
/// for grid/WrapPanel card reordering.
/// </summary>
public class GridDragBehavior : Behavior<Panel>
{
    private Point _dragStartPoint;
    private Point _elementClickOffset;
    private FrameworkElement? _draggedBorder;
    private FrameworkElement? _draggedPanelChild;
    private bool _isDragging;
    private double? _lockedPanelWidth;
    private readonly TimeSpan _animDuration = TimeSpan.FromMilliseconds(220);
    private readonly IEasingFunction _easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private readonly Dictionary<FrameworkElement, TranslateTransform> _transforms = new();
    private AdornerLayer? _adornerLayer;
    private GridDragPlaceholderAdorner? _placeholderAdorner;

    public event Action<List<OrderItem>, OrderItem?>? ReorderComplete;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;
        AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        }
        base.OnDetaching();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging) return;
        _dragStartPoint = e.GetPosition(AssociatedObject);
        _draggedBorder = FindCardElement(e.OriginalSource as DependencyObject);
        if (_draggedBorder != null)
        {
            _draggedPanelChild = FindPanelChildForBorder(_draggedBorder);
            var clickTarget = _draggedPanelChild ?? _draggedBorder;
            _elementClickOffset = e.GetPosition(clickTarget);
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (_isDragging) EndDrag();
            return;
        }

        var pos = e.GetPosition(AssociatedObject);

        if (!_isDragging)
        {
            if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                StartDrag();
            }
        }
        else
        {
            UpdateDrag(pos);
        }
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging) EndDrag();
    }

    private void StartDrag()
    {
        if (_draggedBorder == null || AssociatedObject == null) return;
        _isDragging = true;
        _lockedPanelWidth = AssociatedObject.ActualWidth;

        // use panel child (ContentControl) for transforms when possible
        var transformTarget = _draggedPanelChild ?? _draggedBorder;
        if (transformTarget != null)
        {
            ApplyScaleAndTranslate(transformTarget);
            Panel.SetZIndex(transformTarget, 200);
        }

        Mouse.Capture(AssociatedObject, CaptureMode.SubTree);

        // Prepare placeholder adorner
        _adornerLayer = AdornerLayer.GetAdornerLayer(AssociatedObject as Visual ?? (Visual)AssociatedObject);
        if (_adornerLayer != null && _placeholderAdorner == null)
        {
            _placeholderAdorner = new GridDragPlaceholderAdorner(AssociatedObject);
            _adornerLayer.Add(_placeholderAdorner);
        }
    }

    private void UpdateDrag(Point mousePosition)
    {
        if (_draggedBorder == null || AssociatedObject == null) return;

        // Compute prospective rect of the dragged card (mouse + click offset)
        var sizeTarget = _draggedPanelChild ?? _draggedBorder;
        var panelBounds = new Rect(0, 0, AssociatedObject.ActualWidth, AssociatedObject.ActualHeight);
        const double margin = 6;

        double desiredX = mousePosition.X - _elementClickOffset.X;
        double desiredY = mousePosition.Y - _elementClickOffset.Y;
        desiredX = Math.Max(margin, Math.Min(desiredX, panelBounds.Width - sizeTarget.ActualWidth - margin));
        desiredY = Math.Max(margin, desiredY);

        var draggedRect = new Rect(new Point(desiredX, desiredY), new Size(sizeTarget.ActualWidth, sizeTarget.ActualHeight));

        // Compute insertion index using geometry-based placement
        var wrap = AssociatedObject as WrapPanel;
        int insertionIndex;
        if (wrap != null)
        {
            insertionIndex = CardGridPlacement.CalculateInsertionIndexGrid(AssociatedObject, draggedRect, _draggedBorder, _lockedPanelWidth);
        }
        else
        {
            // fallback: nearest by vertical center
            insertionIndex = FallbackInsertionByCenter(draggedRect);
        }

        // Animate current children to new slot layout including placeholder
        AnimateToPlaceholder(insertionIndex);
    }

    private int FallbackInsertionByCenter(Rect draggedRect)
    {
        if (AssociatedObject == null) return 0;
        var children = GetCardBorders().Where(c => c != _draggedBorder).ToList();
        var centerY = draggedRect.Y + draggedRect.Height / 2.0;
        for (int i = 0; i < children.Count; i++)
        {
            var c = children[i];
            var pos = c.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
            var mid = pos.Y + c.ActualHeight / 2.0;
            if (centerY < mid) return i;
        }
        return children.Count;
    }

    private void AnimateToPlaceholder(int insertionIndex)
    {
        if (AssociatedObject == null || _draggedBorder == null) return;

        // Build placement children list (exclude dragged)
        var allChildren = GetCardBorders();
        var placementChildren = allChildren.Where(c => c != _draggedBorder).ToList();

        // Compute slot layout using existing helper
        CardGridPlacement.ComputeSlotLayout(AssociatedObject, placementChildren, out var slotToChild, out var nextSlot, out var colWidth, out var rowHeights, _lockedPanelWidth);

        // Build ordered child list by start slot
        var childStart = new Dictionary<int, int>();
        foreach (var kv in slotToChild)
        {
            if (!childStart.ContainsKey(kv.Value) || kv.Key < childStart[kv.Value])
                childStart[kv.Value] = kv.Key;
        }

        var ordered = childStart.OrderBy(k => k.Value).Select(k => k.Key).ToList();

        // Insert placeholder at requested insertion index in ordered list
        int insertionPos = Math.Min(Math.Max(0, insertionIndex), ordered.Count);
        ordered.Insert(insertionPos, -1); // -1 = placeholder for dragged

        // Determine columns
        double panelWidth = Math.Max(1, _lockedPanelWidth ?? AssociatedObject.ActualWidth);
        int columns = Math.Max(1, (int)Math.Floor(panelWidth / Math.Max(1, placementChildren.Select(c => c.ActualWidth).DefaultIfEmpty(panelWidth / 2).Average())));

        // Walk ordered and assign slots
        var newStart = new Dictionary<int, int>();
        int cursor = 0;
        int placeholderStart = -1;
        for (int i = 0; i < ordered.Count; i++)
        {
            var idx = ordered[i];
            if (idx == -1)
            {
                // placeholder advances by dragged span (assume 1 or full-span)
                int span = CardGridPlacement.GetSpan(AssociatedObject, _draggedBorder, _lockedPanelWidth);
                if (span > 1 && (cursor % columns) != 0) cursor += (columns - (cursor % columns));
                // record where placeholder starts
                placeholderStart = cursor;
                cursor += span;
                continue;
            }

            var spanChild = CardGridPlacement.GetSpan(AssociatedObject, placementChildren[idx], _lockedPanelWidth);
            if (spanChild > 1 && (cursor % columns) != 0) cursor += (columns - (cursor % columns));
            newStart[idx] = cursor;
            cursor += spanChild;
        }

        // Compute row base Y offsets
        var rowY = new List<double>();
        double cum = 0;
        for (int r = 0; r < rowHeights.Length; r++)
        {
            rowY.Add(cum);
            cum += rowHeights[r];
        }

        // Animate each placement child to its new X/Y
        foreach (var kv in newStart)
        {
            int placementIdx = kv.Key;
            var child = placementChildren[placementIdx];
            int startSlot = kv.Value;
            int row = startSlot / Math.Max(1, columns);
            int col = startSlot % Math.Max(1, columns);
            double targetX = col * colWidth;
            double targetY = row < rowY.Count ? rowY[row] : cum;

            // Determine child's original logical position (try to find starting slot)
            int origStart = slotToChild.Where(k => k.Value == placementIdx).Select(k => k.Key).DefaultIfEmpty(0).Min();
            int origRow = origStart / Math.Max(1, columns);
            int origCol = origStart % Math.Max(1, columns);
            double origX = origCol * colWidth;
            double origY = origRow < rowY.Count ? rowY[origRow] : cum;

            double offsetX = targetX - origX;
            double offsetY = targetY - origY;

            AnimateElementTo(child, offsetX, offsetY);
        }

        // Show placeholder adorner at computed placeholderStart (if available)
        if (_placeholderAdorner != null && placeholderStart >= 0)
        {
            int pRow = placeholderStart / Math.Max(1, columns);
            int pCol = placeholderStart % Math.Max(1, columns);
            double pX = pCol * colWidth;
            double pY = pRow < rowY.Count ? rowY[pRow] : cum;
            int draggedSpan = CardGridPlacement.GetSpan(AssociatedObject, _draggedBorder, _lockedPanelWidth);
            double pW = colWidth * Math.Max(1, draggedSpan);
            double pH = pRow < rowHeights.Length ? rowHeights[pRow] : 40;
            var rect = new Rect(pX, pY, pW, pH);
            _placeholderAdorner.Update(rect);
        }
    }

    private void EndDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;
        Mouse.Capture(null);

        // On drop, determine target item and raise event
        var target = GetDropTarget();
        var items = new List<OrderItem>();
        if (_draggedBorder?.DataContext is OrderItem oi) items.Add(oi);
        if (_draggedBorder?.DataContext is OrderItemGroup og) items.AddRange(og.Members);

        ReorderComplete?.Invoke(items, target);

        // Clear transforms and reset
        foreach (var kv in _transforms.ToList())
        {
            var t = kv.Value;
            t.BeginAnimation(TranslateTransform.XProperty, null);
            t.BeginAnimation(TranslateTransform.YProperty, null);
            kv.Key.RenderTransform = null;
        }
        _transforms.Clear();
        if (_draggedPanelChild != null) Panel.SetZIndex(_draggedPanelChild, 0);
        _lockedPanelWidth = null;
        _draggedBorder = null;
        _draggedPanelChild = null;

        // Remove and clear placeholder adorner
        if (_placeholderAdorner != null && _adornerLayer != null)
        {
            _adornerLayer.Remove(_placeholderAdorner);
            _placeholderAdorner = null;
        }
        _adornerLayer = null;
    }

    private OrderItem? GetDropTarget()
    {
        if (AssociatedObject == null || _draggedBorder == null) return null;
        var children = GetCardBorders().Where(c => c != _draggedBorder).ToList();
        // pick nearest by center to dragged element center
        // Use the final transform if present
        var draggedCenter = (_draggedPanelChild ?? _draggedBorder).TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
        draggedCenter = new Point(draggedCenter.X + (_draggedPanelChild ?? _draggedBorder).ActualWidth / 2.0, draggedCenter.Y + (_draggedPanelChild ?? _draggedBorder).ActualHeight / 2.0);
        double bestDist = double.MaxValue;
        FrameworkElement? best = null;
        foreach (var c in children)
        {
            var p = c.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
            var center = new Point(p.X + c.ActualWidth / 2.0, p.Y + c.ActualHeight / 2.0);
            double dx = center.X - draggedCenter.X; double dy = center.Y - draggedCenter.Y;
            double d = dx * dx + dy * dy;
            if (d < bestDist) { bestDist = d; best = c; }
        }

        if (best == null) return null;
        if (best.DataContext is OrderItem oi) return oi;
        if (best.DataContext is OrderItemGroup og) return og.First;
        return null;
    }

    private void ApplyScaleAndTranslate(FrameworkElement element)
    {
        var tg = new TransformGroup();
        var st = new ScaleTransform(1.02, 1.02) { CenterX = element.ActualWidth / 2, CenterY = element.ActualHeight / 2 };
        var tt = new TranslateTransform();
        tg.Children.Add(st);
        tg.Children.Add(tt);
        element.RenderTransform = tg;
        if (!_transforms.ContainsKey(element)) _transforms[element] = tt;
    }

    private void AnimateElementTo(FrameworkElement element, double offsetX, double offsetY)
    {
        var transform = GetOrCreateTranslate(element);
        var ax = new DoubleAnimation { To = offsetX, Duration = _animDuration, EasingFunction = _easing };
        var ay = new DoubleAnimation { To = offsetY, Duration = _animDuration, EasingFunction = _easing };
        transform.BeginAnimation(TranslateTransform.XProperty, ax);
        transform.BeginAnimation(TranslateTransform.YProperty, ay);
    }

    private TranslateTransform GetOrCreateTranslate(FrameworkElement element)
    {
        if (_transforms.TryGetValue(element, out var t)) return t;
        TranslateTransform tt = new();
        if (element.RenderTransform is TransformGroup g)
        {
            g.Children.Add(tt);
        }
        else if (element.RenderTransform != null && element.RenderTransform != Transform.Identity)
        {
            var group = new TransformGroup();
            group.Children.Add(element.RenderTransform);
            group.Children.Add(tt);
            element.RenderTransform = group;
        }
        else
        {
            element.RenderTransform = tt;
        }
        _transforms[element] = tt;
        return tt;
    }

    private IEnumerable<FrameworkElement> GetCardBorders()
    {
        if (AssociatedObject == null) return Enumerable.Empty<FrameworkElement>();
        var list = new List<FrameworkElement>();
        foreach (var child in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (child.Visibility != Visibility.Visible) continue;
            var b = FindVisualChildOfType<Border>(child);
            if (b != null && (b.DataContext is OrderItem || b.DataContext is OrderItemGroup))
                list.Add(b);
        }
        return list;
    }

    private FrameworkElement? FindCardElement(DependencyObject? src)
    {
        var cur = src;
        while (cur != null)
        {
            if (cur is Border border && (border.DataContext is OrderItem || border.DataContext is OrderItemGroup))
                return border;
            if (cur is ContentControl cc && (cc.DataContext is OrderItem || cc.DataContext is OrderItemGroup))
            {
                var child = FindVisualChildOfType<Border>(cc);
                if (child != null) return child;
            }
            if (cur == AssociatedObject) break;
            cur = VisualTreeHelper.GetParent(cur);
        }
        return null;
    }

    private FrameworkElement? FindPanelChildForBorder(FrameworkElement border)
    {
        if (AssociatedObject == null) return null;
        var data = border.DataContext;
        foreach (var child in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            var b = FindVisualChildOfType<Border>(child);
            if (b == null) continue;
            if (ReferenceEquals(b.DataContext, data)) return child;
            if (b.DataContext is OrderItem bi && data is OrderItem di && bi.Id == di.Id) return child;
            if (b.DataContext is OrderItemGroup bg && data is OrderItemGroup dg)
            {
                if (bg.Members.Count > 0 && dg.Members.Count > 0 && bg.First.Id == dg.First.Id) return child;
            }
        }
        return FindPanelChild(border);
    }

    private FrameworkElement? FindPanelChild(DependencyObject? element)
    {
        if (AssociatedObject == null || element == null) return null;
        var cur = element;
        while (cur != null)
        {
            var parent = VisualTreeHelper.GetParent(cur);
            if (parent == AssociatedObject && cur is FrameworkElement fe) return fe;
            cur = parent;
        }
        return null;
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
