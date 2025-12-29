using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace SOUP.Behaviors;

/// <summary>
/// Behavior that enables drag-drop reordering of items in a ListBox with iOS-like slide animation.
/// Uses mouse capture instead of WPF DragDrop for smoother visuals.
/// </summary>
public class ListBoxDragDropBehavior : Behavior<ListBox>
{
    private Point _startPoint;
    private object? _draggedItem;
    private int _draggedIndex = -1;
    private int _currentPreviewIndex = -1;
    private ListBoxItem? _draggedListBoxItem;
    private bool _isDragging;
    private readonly Dictionary<ListBoxItem, TranslateTransform> _transforms = new();

    public static readonly DependencyProperty OnReorderProperty =
        DependencyProperty.Register(nameof(OnReorder), typeof(Action), typeof(ListBoxDragDropBehavior));

    /// <summary>
    /// Action to invoke when items are reordered.
    /// </summary>
    public Action? OnReorder
    {
        get => (Action?)GetValue(OnReorderProperty);
        set => SetValue(OnReorderProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        AssociatedObject.MouseLeave += OnMouseLeave;
        AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        AssociatedObject.MouseLeave -= OnMouseLeave;
        AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
        CancelDrag();
    }

    private void HookWindowWheelHandler()
    {
        // Removed: window-level debug wheel hook (no longer needed)
    }

    private void OnPreviewMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        // If the source is a scrollable control (TextBox, RichTextBox, inner ScrollViewer), let it handle the event.
        DependencyObject? src = e.OriginalSource as DependencyObject;
        while (src != null)
        {
            if (src is ScrollViewer || src is TextBox || src is RichTextBox)
            {
                return; // let the inner control handle scrolling
            }
            src = GetParentSafe(src);
        }

        // Otherwise, find nearest parent ScrollViewer from the event source
        var sv = e.OriginalSource is DependencyObject srcObj ? FindAncestorSafe<ScrollViewer>(srcObj) : null;

        // Fallback to ListBox ancestor if none found
        if (sv == null)
            sv = FindAncestor<ScrollViewer>(AssociatedObject);

        if (sv != null)
        {
            // Adjust the offset (Delta is in multiples of 120 per notch)
            double newOffset = sv.VerticalOffset - (e.Delta / 3.0);
            newOffset = Math.Max(0, Math.Min(newOffset, sv.ExtentHeight - sv.ViewportHeight));
            sv.ScrollToVerticalOffset(newOffset);
            e.Handled = true;
        }

    // Safely get a parent for DependencyObjects that may not be Visuals (eg FlowDocument elements like Paragraph)
    private DependencyObject? GetParentSafe(DependencyObject child)
    {
        if (child == null) return null;
        // Use VisualTreeHelper for Visual/Visual3D, otherwise fall back to LogicalTreeHelper
        if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
        {
            try
            {
                return VisualTreeHelper.GetParent(child);
            }
            catch
            {
                // Fall through to logical parent
            }
        }

        try
        {
            return LogicalTreeHelper.GetParent(child);
        }
        catch
        {
            return null;
        }
    }

    private T? FindAncestorSafe<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T t) return t;
            current = GetParentSafe(current);
        }
        return null;
    }

    // Searches descendants (visual + logical) for a descendant of type T using BFS, safe against non-visual nodes.
    private T? FindDescendantSafe<T>(DependencyObject? start) where T : DependencyObject
    {
        if (start == null) return null;
        var q = new Queue<DependencyObject>();
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur is T found) return found;

            // Enqueue visual children when available
            try
            {
                int count = VisualTreeHelper.GetChildrenCount(cur);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(cur, i);
                    if (child != null)
                        q.Enqueue(child);
                }
            }
            catch { }

            // Enqueue logical children when available
            try
            {
                foreach (var logical in LogicalTreeHelper.GetChildren(cur))
                {
                    if (logical is DependencyObject dob)
                        q.Enqueue(dob);
                }
            }
            catch { }
        }

        return null;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't interfere if already dragging
        if (_isDragging)
            return;
            
        _startPoint = e.GetPosition(AssociatedObject);
        
        // Find the item being clicked
        var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (item != null)
        {
            _draggedItem = item.DataContext;
            _draggedIndex = AssociatedObject.Items.IndexOf(_draggedItem);
            _draggedListBoxItem = item;
        }
        
        // Don't mark as handled - let normal selection work
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null || _draggedListBoxItem == null)
            return;

        var currentPoint = e.GetPosition(AssociatedObject);

        if (!_isDragging)
        {
            var diff = _startPoint - currentPoint;
            
            // Check if we've moved far enough to start a drag
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                StartDrag();
            }
        }

        if (_isDragging)
        {
            UpdateDrag(currentPoint);
        }
    }

    private void StartDrag()
    {
        if (_draggedListBoxItem == null)
            return;

        _isDragging = true;
        _currentPreviewIndex = _draggedIndex;
        
        // Capture mouse to receive events even outside the control
        Mouse.Capture(AssociatedObject, CaptureMode.SubTree);

        // Bring dragged item to front and add slight scale effect
        _draggedListBoxItem.SetValue(Panel.ZIndexProperty, 100);
        
        // Add a subtle shadow/scale effect to the dragged item
        var scaleTransform = new ScaleTransform(1.02, 1.02, 
            _draggedListBoxItem.ActualWidth / 2, _draggedListBoxItem.ActualHeight / 2);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(new TranslateTransform());
        transformGroup.Children.Add(scaleTransform);
        _draggedListBoxItem.RenderTransform = transformGroup;
        _transforms[_draggedListBoxItem] = (TranslateTransform)transformGroup.Children[0];
    }

    private void UpdateDrag(Point mousePos)
    {
        if (_draggedListBoxItem == null || !_isDragging)
            return;

        // Calculate how far we've moved from the start
        double offsetY = mousePos.Y - _startPoint.Y;
        
        // Update dragged item position
        if (_transforms.TryGetValue(_draggedListBoxItem, out var dragTransform))
        {
            dragTransform.Y = offsetY;
        }

        // Calculate which index we're hovering over using item midpoints (more accurate for varying heights)
        int previewIndex = CalculatePreviewIndexFromPosition(mousePos);

        // Update animations if preview index changed
        if (previewIndex != _currentPreviewIndex)
        {
            _currentPreviewIndex = previewIndex;
            AnimateOtherItemsToPreviewPositions();
        }
    }

    private void AnimateOtherItemsToPreviewPositions()
    {
        if (_draggedIndex < 0 || _currentPreviewIndex < 0)
            return;

        var itemHeight = GetAverageItemHeight();
        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        for (int i = 0; i < AssociatedObject.Items.Count; i++)
        {
            // Skip the dragged item
            if (i == _draggedIndex)
                continue;

            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null)
                continue;

            // Get or create transform
            if (!_transforms.TryGetValue(container, out var transform))
            {
                transform = new TranslateTransform();
                _transforms[container] = transform;
                container.RenderTransform = transform;
            }

            // Calculate target offset
            double targetY = 0;
            
            if (_draggedIndex < _currentPreviewIndex)
            {
                // Dragging down: items between old and new position slide up
                if (i > _draggedIndex && i <= _currentPreviewIndex)
                {
                    targetY = -itemHeight;
                }
            }
            else if (_draggedIndex > _currentPreviewIndex)
            {
                // Dragging up: items between new and old position slide down
                if (i >= _currentPreviewIndex && i < _draggedIndex)
                {
                    targetY = itemHeight;
                }
            }

            // Animate to target
            var animation = new DoubleAnimation
            {
                To = targetY,
                Duration = duration,
                EasingFunction = ease
            };
            transform.BeginAnimation(TranslateTransform.YProperty, animation);
        }
    }

    private int CalculatePreviewIndexFromPosition(Point mousePos)
    {
        // Iterate items and use the container top/bottom edges (including margin) to determine insertion point
        int itemCount = AssociatedObject.Items.Count;
        for (int i = 0; i < itemCount; i++)
        {
            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;

            // Get container top-left relative to the ListBox
            var topLeft = container.TransformToVisual(AssociatedObject).Transform(new Point(0, 0));
            double top = topLeft.Y;
            double bottom = top + container.ActualHeight + container.Margin.Bottom;

            // If pointer is above the top border of this item, insert before it
            if (mousePos.Y < top)
            {
                return i;
            }

            // If pointer is within this item's vertical bounds, return this index
            if (mousePos.Y >= top && mousePos.Y <= bottom)
            {
                return i;
            }
        }

        // If below all items, return last index
        return Math.Max(0, itemCount - 1);
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            FinishDrag();
            e.Handled = true; // Only handle if we were dragging
        }
        else
        {
            // Not dragging - just reset state, don't interfere with normal click
            _draggedItem = null;
            _draggedIndex = -1;
            _draggedListBoxItem = null;
            // Don't mark as handled - let normal click/selection work
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        // Only cancel if we're not dragging (if dragging, we have mouse capture)
        if (!_isDragging)
        {
            CancelDrag();
        }
    }

    private void FinishDrag()
    {
        if (!_isDragging || _draggedIndex < 0 || _currentPreviewIndex < 0)
        {
            CancelDrag();
            return;
        }

        Mouse.Capture(null);

        var newIndex = _currentPreviewIndex;
        var oldIndex = _draggedIndex;

        // Reset all transforms immediately (no animation for the final snap)
        ResetAllTransformsImmediate();

        // Move the item in the data source if position changed
        if (newIndex != oldIndex && AssociatedObject.ItemsSource is IList list)
        {
            var selectedItem = AssociatedObject.SelectedItem;
            var item = list[oldIndex];

            // Adjust insert index because removing the old item shifts indexes
            int insertIndex = newIndex;
            if (insertIndex > oldIndex)
                insertIndex--;

            // Clamp insertIndex to [0, list.Count]
            insertIndex = Math.Max(0, Math.Min(insertIndex, list.Count));

            list.RemoveAt(oldIndex);
            list.Insert(insertIndex, item);
            AssociatedObject.SelectedItem = selectedItem;
            OnReorder?.Invoke();
        }

        _isDragging = false;
        _draggedItem = null;
        _draggedIndex = -1;
        _draggedListBoxItem = null;
        _currentPreviewIndex = -1;
    }

    private void CancelDrag()
    {
        Mouse.Capture(null);
        
        if (_isDragging)
        {
            ResetAllTransformsAnimated();
        }
        else
        {
            ResetAllTransformsImmediate();
        }

        _isDragging = false;
        _draggedItem = null;
        _draggedIndex = -1;
        _draggedListBoxItem = null;
        _currentPreviewIndex = -1;
    }

    private void ResetAllTransformsImmediate()
    {
        foreach (var kvp in _transforms)
        {
            kvp.Value.BeginAnimation(TranslateTransform.YProperty, null);
            kvp.Key.RenderTransform = null;
            kvp.Key.SetValue(Panel.ZIndexProperty, 0);
        }
        _transforms.Clear();
    }

    private void ResetAllTransformsAnimated()
    {
        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        foreach (var kvp in _transforms)
        {
            var animation = new DoubleAnimation
            {
                To = 0,
                Duration = duration,
                EasingFunction = ease
            };
            kvp.Value.BeginAnimation(TranslateTransform.YProperty, animation);
        }

        // Clear after animation completes
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            foreach (var kvp in _transforms)
            {
                kvp.Key.RenderTransform = null;
                kvp.Key.SetValue(Panel.ZIndexProperty, 0);
            }
            _transforms.Clear();
        };
        timer.Start();
    }

    private double GetAverageItemHeight()
    {
        for (int i = 0; i < AssociatedObject.Items.Count; i++)
        {
            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container != null && container.ActualHeight > 0)
            {
                return container.ActualHeight;
            }
        }
        return 60;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
