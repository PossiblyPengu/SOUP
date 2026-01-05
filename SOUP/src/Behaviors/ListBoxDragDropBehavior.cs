using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;
using System.Windows.Documents;
using SOUP.Features.OrderLog.Helpers;

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
    private int _dragStartIndex = -1;
    private int _currentPreviewIndex = -1;
    private ListBoxItem? _draggedListBoxItem;
    private bool _isDragging;
    private bool _isLinkMode;
    private readonly Dictionary<ListBoxItem, TranslateTransform> _transforms = new();
    private AdornerLayer? _adornerLayer;
    // insertion adorner removed; sliding animations are used instead
    private FloatingAdorner? _floatingAdorner;
    private Point _dragOffset;
    private double? _floatingFixedLeft;
    private Point _lastMousePos;

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
        // Only start potential drag when pressing the visible handle (Tag="DragHandle")
        var srcCheck = e.OriginalSource as DependencyObject;
        bool isHandle = false;
        while (srcCheck != null)
        {
            if (srcCheck is FrameworkElement fe && fe.Tag is string t && t == "DragHandle") { isHandle = true; break; }
            srcCheck = GetParentSafe(srcCheck);
        }
        if (!isHandle)
            return;
        _startPoint = e.GetPosition(AssociatedObject);
        // detect ctrl for link-mode (Ctrl-drag to link orders)
        try
        {
            _isLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (_isLinkMode) _currentPreviewIndex = -1; // don't show reorder preview for linking
        }
        catch { _isLinkMode = false; }
        try
        {
            var srcType = e.OriginalSource?.GetType().Name ?? "(null)";
            
        }
        catch { /* Intentionally ignored: debug logging only */ }
        
        // Find the item being clicked
        var item = FindAncestor<ListBoxItem>((DependencyObject?)e.OriginalSource);
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
        try
        {
            
        }
        catch { }

        if (!_isDragging)
        {
            var diff = _startPoint - currentPoint;

            // Start drag only on vertical movement (disable horizontal dragging)
            if (Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
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

        try {  } catch { }
        _isDragging = true;
        _dragStartIndex = _draggedIndex;
        _currentPreviewIndex = _dragStartIndex;
        
        // Capture mouse to receive events even outside the control
        Mouse.Capture(AssociatedObject, CaptureMode.SubTree);

        // Bring dragged item to front; we'll hide the original only after creating a floating snapshot
        _draggedListBoxItem.SetValue(Panel.ZIndexProperty, 100);
        // prepare adorner layer. prefer the window content so the floating adorner renders above list items
        try
        {
            UIElement host = AssociatedObject;
            var window = FindAncestorSafe<Window>(AssociatedObject);
            if (window != null && window.Content is UIElement wc)
            {
                host = wc;
            }
            else
            {
                // fallback to nearest ScrollViewer so the adorner aligns with the viewport
                var sv = FindAncestorSafe<ScrollViewer>(AssociatedObject) as UIElement;
                if (sv != null) host = sv;
            }

            _adornerLayer = AdornerLayer.GetAdornerLayer(host);
            if (_adornerLayer != null)
            {
                // Create a bitmap snapshot of the dragged item and show it in a floating adorner
                try
                {
                    // Use Actual size when available, otherwise fall back to DesiredSize
                    double itemW = _draggedListBoxItem.ActualWidth;
                    double itemH = _draggedListBoxItem.ActualHeight;
                    if (itemW <= 1 || itemH <= 1)
                    {
                        itemW = Math.Max(itemW, _draggedListBoxItem.DesiredSize.Width);
                        itemH = Math.Max(itemH, _draggedListBoxItem.DesiredSize.Height);
                    }

                    // NOTE: snapshot creation (including special-case for grouped items) handled below
                        if (itemW > 1 && itemH > 1)
                        {
                            try
                            {
                                // Special-case rendering for grouped items: instantiate the merged template into an off-screen presenter
                                ImageSource? bitmapSource = null;
                                bool usedTemplate = false;

                                try
                                {
                                    // Prefer rendering the inner ContentControl's DataTemplate if present
                                    try
                                    {
                                        var innerContent = FindDescendantSafe<ContentControl>(_draggedListBoxItem);
                                            if (innerContent != null && innerContent.ContentTemplate != null)
                                        {
                                            var presenter = new ContentPresenter { Content = innerContent.Content, ContentTemplate = innerContent.ContentTemplate };
                                            // Ensure bindings inside the template resolve by setting DataContext to the inner content's DataContext
                                            try { presenter.DataContext = innerContent.DataContext; } catch { }
                                            presenter.Measure(new Size(itemW, itemH));
                                            presenter.Arrange(new Rect(0, 0, itemW, itemH));

                                            // If the template contains a RichTextBox that expects note XAML to be loaded
                                            // by the view's Loaded handler, populate it explicitly so the off-screen
                                            // presenter renders the note content.
                                            try
                                            {
                                                var rtbInPresenter = FindDescendantSafe<System.Windows.Controls.RichTextBox>(presenter);
                                                if (rtbInPresenter != null)
                                                {
                                                    try { TextFormattingHelper.LoadNoteContent(rtbInPresenter); } catch { }
                                                }
                                            }
                                            catch { }

                                            try
                                            {
                                                // Allow the presenter to perform layout/render passes before snapshotting
                                                try { presenter.ApplyTemplate(); presenter.UpdateLayout(); } catch { }
                                                try { presenter.Dispatcher.Invoke(() => { presenter.UpdateLayout(); }, DispatcherPriority.Render, System.Threading.CancellationToken.None); } catch { }

                                                try
                                                {
                                                    var rtbInPresenter = FindDescendantSafe<System.Windows.Controls.RichTextBox>(presenter);
                                                    if (rtbInPresenter != null)
                                                    {
                                                        try { TextFormattingHelper.LoadNoteContent(rtbInPresenter); } catch { }
                                                    }
                                                }
                                                catch { }

                                                var dv = new DrawingVisual();
                                                using (var ctx = dv.RenderOpen())
                                                {
                                                    var vb = new VisualBrush(presenter);
                                                    ctx.DrawRectangle(vb, null, new Rect(0, 0, itemW, itemH));
                                                }

                                                var rtb2 = new RenderTargetBitmap((int)Math.Ceiling(itemW), (int)Math.Ceiling(itemH), 96, 96, PixelFormats.Pbgra32);
                                                rtb2.Render(dv);
                                                bitmapSource = rtb2;
                                            }
                                            catch { }
                                            usedTemplate = true;
                                        }
                                    }
                                    catch { }

                                    if (!usedTemplate && _draggedItem != null && _draggedItem.GetType().Name == "OrderItemGroup")
                                    {
                                        // Try to find the merged template resource from the visual tree
                                        object? tmplObj = null;
                                        try { tmplObj = AssociatedObject.TryFindResource("OrderItemGroupMergedTemplate"); } catch { tmplObj = null; }
                                        if (tmplObj is DataTemplate dt)
                                        {
                                            var presenter = new ContentPresenter { Content = _draggedItem, ContentTemplate = dt };
                                            try { presenter.DataContext = _draggedItem; } catch { }
                                            presenter.Measure(new Size(itemW, itemH));
                                            presenter.Arrange(new Rect(0, 0, itemW, itemH));

                                            var dv = new DrawingVisual();
                                            using (var ctx = dv.RenderOpen())
                                            {
                                                var vb = new VisualBrush(presenter);
                                                ctx.DrawRectangle(vb, null, new Rect(0, 0, itemW, itemH));
                                            }

                                            var rtb2 = new RenderTargetBitmap((int)Math.Ceiling(itemW), (int)Math.Ceiling(itemH), 96, 96, PixelFormats.Pbgra32);
                                            rtb2.Render(dv);
                                            bitmapSource = rtb2;
                                            usedTemplate = true;
                                        }
                                    }
                                }
                                catch { }

                                if (!usedTemplate)
                                {
                                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(itemW), (int)Math.Ceiling(itemH), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                                    rtb.Render(_draggedListBoxItem);
                                    bitmapSource = rtb;
                                }

                                // If bitmap rendering failed or produced no content, fall back to a textual snapshot
                                bool needsTextFallback = false;
                                try
                                {
                                    if (bitmapSource == null) needsTextFallback = true;
                                    else if (bitmapSource is RenderTargetBitmap rtbCheck)
                                    {
                                        if (rtbCheck.PixelWidth == 0 || rtbCheck.PixelHeight == 0)
                                        {
                                            needsTextFallback = true;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                int bitsPerPixel = rtbCheck.Format.BitsPerPixel;
                                                int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
                                                int stride = rtbCheck.PixelWidth * bytesPerPixel;
                                                var pixels = new byte[stride * rtbCheck.PixelHeight];
                                                rtbCheck.CopyPixels(pixels, stride, 0);
                                                bool anyNonZero = false;
                                                for (int pi = 0; pi + bytesPerPixel - 1 < pixels.Length; pi += bytesPerPixel)
                                                {
                                                    // check any channel or alpha is non-zero
                                                    bool allZero = true;
                                                    for (int c = 0; c < bytesPerPixel; c++)
                                                    {
                                                        if (pixels[pi + c] != 0) { allZero = false; break; }
                                                    }
                                                    if (!allZero) { anyNonZero = true; break; }
                                                }
                                                if (!anyNonZero)
                                                {
                                                    needsTextFallback = true;
                                                        try
                                                        {
                                                            // Log a small sample of the raw pixels for diagnostics
                                                            int sampleLen = Math.Min(32, pixels.Length);
                                                            try
                                                            {
                                                                var sb = new System.Text.StringBuilder();
                                                                for (int si = 0; si < sampleLen; si++) sb.AppendFormat("{0:X2}", pixels[si]);
                                                                
                                                            }
                                                            catch { /* sample failed */ }
                                                        }
                                                        catch { }
                                                }
                                            }
                                            catch { needsTextFallback = true; }
                                        }
                                    }
                                }
                                catch { needsTextFallback = true; }

                                if (needsTextFallback)
                                {
                                    try
                                    {
                                        string title = string.Empty;
                                        string content = string.Empty;
                                        if (_draggedItem != null)
                                        {
                                            try { var tprop = _draggedItem.GetType().GetProperty("NoteTitle"); if (tprop != null) title = (tprop.GetValue(_draggedItem) ?? string.Empty).ToString() ?? string.Empty; } catch { /* Intentionally ignored: reflection fallback */ }
                                            try { var cprop = _draggedItem.GetType().GetProperty("NoteContent"); if (cprop != null) content = (cprop.GetValue(_draggedItem) ?? string.Empty).ToString() ?? string.Empty; } catch { /* Intentionally ignored: reflection fallback */ }
                                        }

                                        // Compose a simple visual containing title and content
                                        var border = new Border { Background = Brushes.White, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
                                        var sp = new StackPanel { Margin = new Thickness(6) };
                                        if (!string.IsNullOrEmpty(title)) sp.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                                        sp.Children.Add(new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,0) });
                                        border.Child = sp;
                                        border.Measure(new Size(itemW, itemH));
                                        border.Arrange(new Rect(0, 0, itemW, itemH));

                                        var dv2 = new DrawingVisual();
                                        using (var ctx2 = dv2.RenderOpen())
                                        {
                                            var vb2 = new VisualBrush(border);
                                            ctx2.DrawRectangle(vb2, null, new Rect(0, 0, itemW, itemH));
                                        }

                                        var rtbFb = new RenderTargetBitmap((int)Math.Ceiling(itemW), (int)Math.Ceiling(itemH), 96, 96, PixelFormats.Pbgra32);
                                        rtbFb.Render(dv2);
                                        bitmapSource = rtbFb;
                                    }
                                    catch { }
                                }

                                if (bitmapSource != null)
                                {
                                    _floatingAdorner = new FloatingAdorner(host, bitmapSource, itemW, itemH);
                                    _adornerLayer.Add(_floatingAdorner);

                                    // compute pointer offset within the item so the snapshot tracks the pointer naturally
                                    var itemTopLeft = _draggedListBoxItem.TransformToVisual(AssociatedObject).Transform(new Point(0, 0));
                                    _dragOffset = new Point(_startPoint.X - itemTopLeft.X, _startPoint.Y - itemTopLeft.Y);

                                    // position adorner initially and lock X to avoid horizontal dragging
                                    var adorned = _floatingAdorner.AdornedElement as UIElement ?? AssociatedObject;
                                    var posInAdorned = AssociatedObject.TransformToVisual(adorned).Transform(_startPoint);
                                    var left = posInAdorned.X - _dragOffset.X;
                                    var top = posInAdorned.Y - _dragOffset.Y;
                                    _floatingFixedLeft = left;
                                    _floatingAdorner.UpdatePosition(left, top);

                                    // Hide the original item to avoid a visible duplicate during drag
                                    try
                                    {
                                        if (_draggedListBoxItem != null)
                                            _draggedListBoxItem.Opacity = 0.0;
                                    }
                                    catch { }
                                }

                                // keep the original visible (don't set Opacity=0) to avoid container-recycling visual issues
                            }
                            catch
                            {
                                _floatingAdorner = null;
                            }
                        }
                    else
                    {
                        _floatingAdorner = null;
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private void UpdateDrag(Point mousePos)
    {
        if (_draggedListBoxItem == null || !_isDragging)
            return;

        // Calculate how far we've moved from the start
        double offsetY = mousePos.Y - _startPoint.Y;
        try {  } catch { }
        
        // Update dragged item position
        if (_transforms.TryGetValue(_draggedListBoxItem, out var dragTransform))
        {
            dragTransform.Y = offsetY;
        }

        // Move floating snapshot with the pointer
            if (_floatingAdorner != null)
            {
                try
                {
                    var adorned = _floatingAdorner.AdornedElement as UIElement ?? AssociatedObject;
                    var posInAdorned = AssociatedObject.TransformToVisual(adorned).Transform(mousePos);
                    var topPos = posInAdorned.Y - _dragOffset.Y;
                    // keep X locked to initial left to disable horizontal movement
                    var leftPos = _floatingFixedLeft ?? (posInAdorned.X - _dragOffset.X);
                    _floatingAdorner.UpdatePosition(leftPos, topPos);
                }
                catch { }
            }

        // Determine insertion index using midpoint calculation (insertion semantics)
        int hoverIndex = CalculatePreviewIndexFromPosition(mousePos);
        _lastMousePos = mousePos;
        try {  } catch { }

        // If hovering over a different item than the drag start, show a swap preview
        if (hoverIndex >= 0 && hoverIndex != _currentPreviewIndex)
        {
            _currentPreviewIndex = hoverIndex;
            // Use insertion midpoint behavior: animate other items to make room for insertion at hoverIndex
            AnimateOtherItemsToPreviewPositions();
            // (insertion adorner removed) sliding animations show preview instead
        }
    }

    // Returns the index of the item whose bounds contain the given mouse position, or -1.
    private int GetIndexAtPosition(Point mousePos)
    {
        for (int i = 0; i < AssociatedObject.Items.Count; i++)
        {
            if (i == _dragStartIndex) continue; // skip original dragged slot

            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;

            var topLeft = container.TransformToVisual(AssociatedObject).Transform(new Point(0, 0));
            var rect = new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));
            if (rect.Contains(mousePos))
                return i;
        }
        return -1;
    }

    // After the dragged item has been removed from the underlying list, compute insertion index
    // by checking midpoints of the remaining containers.
    private int GetIndexAtPositionAfterRemoval(Point mousePos)
    {
        for (int i = 0; i < AssociatedObject.Items.Count; i++)
        {
            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;

            var topLeft = container.TransformToVisual(AssociatedObject).Transform(new Point(0, 0));
            double mid = topLeft.Y + (container.ActualHeight / 2.0);
            if (mousePos.Y < mid)
                return i;
        }
        return AssociatedObject.Items.Count;
    }

    // Animate the item at targetIndex to move into the dragged item's original slot (visual swap preview)
    private void AnimateSwapPreview(int draggedIndex, int targetIndex)
    {
        // Clear existing transforms on all items except the dragged visual (which follows the pointer)
        foreach (var kvp in _transforms)
        {
            if (kvp.Key != _draggedListBoxItem)
            {
                kvp.Value.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }

        var draggedContainer = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(draggedIndex) as ListBoxItem;
        var targetContainer = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(targetIndex) as ListBoxItem;
        if (draggedContainer == null || targetContainer == null) return;

        // Ensure transforms exist for target container
        if (!_transforms.TryGetValue(targetContainer, out var targetTransform))
        {
            targetTransform = new TranslateTransform();
            _transforms[targetContainer] = targetTransform;
            targetContainer.RenderTransform = targetTransform;
        }

        // Compute vertical distance to move target into dragged slot
        var draggedTop = draggedContainer.TransformToVisual(AssociatedObject).Transform(new Point(0, 0)).Y;
        var targetTop = targetContainer.TransformToVisual(AssociatedObject).Transform(new Point(0, 0)).Y;
        double delta = draggedTop - targetTop;

        var animation = new DoubleAnimation
        {
            To = delta,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        targetTransform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void AnimateOtherItemsToPreviewPositions()
    {
        if (_draggedIndex < 0 || _currentPreviewIndex < 0)
            return;

        // Use the floating snapshot height when available (handles grouped/double cards), otherwise fall back to the dragged container ActualHeight
        double itemHeight;
        try
        {
            if (_floatingAdorner != null)
            {
                try { itemHeight = _floatingAdorner.AdornedHeight; }
                catch { itemHeight = GetAverageItemHeight(); }
            }
            else
            {
                var draggedContainer = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(_draggedIndex) as ListBoxItem;
                itemHeight = (draggedContainer != null && draggedContainer.ActualHeight > 0) ? draggedContainer.ActualHeight : GetAverageItemHeight();
            }
        }
        catch
        {
            itemHeight = GetAverageItemHeight();
        }
        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        for (int i = 0; i < AssociatedObject.Items.Count; i++)
        {
            if (i == _draggedIndex)
                continue;

            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null)
                continue;

            if (!_transforms.TryGetValue(container, out var transform))
            {
                transform = new TranslateTransform();
                _transforms[container] = transform;
                container.RenderTransform = transform;
            }

            double targetY = 0;
            if (_draggedIndex < _currentPreviewIndex)
            {
                if (i > _draggedIndex && i <= _currentPreviewIndex)
                    targetY = -itemHeight;
            }
            else if (_draggedIndex > _currentPreviewIndex)
            {
                if (i >= _currentPreviewIndex && i < _draggedIndex)
                    targetY = itemHeight;
            }

            var animation = new DoubleAnimation
            {
                To = targetY,
                Duration = duration,
                EasingFunction = ease
            };
            transform.BeginAnimation(TranslateTransform.YProperty, animation);
        }
        // Ensure ZIndex ordering so animated items don't cover the floating snapshot
        try
        {
            for (int i = 0; i < AssociatedObject.Items.Count; i++)
            {
                var cont = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (cont == null) continue;
                if (cont == _draggedListBoxItem)
                    cont.SetValue(Panel.ZIndexProperty, 200);
                else
                    cont.SetValue(Panel.ZIndexProperty, 0);
            }
        }
        catch { }
    }

    private int CalculatePreviewIndexFromPosition(Point mousePos)
    {
        int itemCount = AssociatedObject.Items.Count;
        if (itemCount == 0) return 0;

        for (int i = 0; i < itemCount; i++)
        {
            if (i == _draggedIndex) continue;

            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;

            var topLeft = container.TransformToVisual(AssociatedObject).Transform(new Point(0, 0));
            double top = topLeft.Y;
            double mid = top + (container.ActualHeight / 2.0);

            if (mousePos.Y < mid)
                return i;
        }

        return itemCount;
    }

    // Return the underlying OrderItem (if any) for the ListBox item under the given mouse position.
    private Features.OrderLog.Models.OrderItem? GetOrderItemAtPosition(Point mousePos)
    {
        for (int i = 0; i < AssociatedObject.Items.Count; i++)
        {
            if (i == _dragStartIndex) continue; // skip the dragged original

            var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;

            var topLeft = container.TransformToVisual(AssociatedObject).Transform(new Point(0, 0));
            var rect = new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));
            if (!rect.Contains(mousePos)) continue;

            // DataContext may be an OrderItem or an OrderItemGroup
            if (container.DataContext is Features.OrderLog.Models.OrderItem oi)
                return oi;
            if (container.DataContext is Features.OrderLog.ViewModels.OrderItemGroup grp)
                return grp.First;
        }
        return null;
    }

    // insertion adorner removed: visual preview is communicated by sliding animations

    private void HideAdorners()
    {
        try
        {
            if (_adornerLayer != null)
            {
                if (_floatingAdorner != null)
                {
                    _adornerLayer.Remove(_floatingAdorner);
                }
            }
            _floatingAdorner = null;
            _adornerLayer = null;
        }
        catch { }
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
        if (!_isDragging || _draggedIndex < 0)
        {
            CancelDrag();
            return;
        }

        HideAdorners();
        Mouse.Capture(null);

        var newIndex = _currentPreviewIndex;
        var oldIndex = _dragStartIndex;

        // Reset all transforms immediately (no animation for the final snap)
        ResetAllTransformsImmediate();

        // Restore original item's visibility
        try { if (_draggedListBoxItem != null) _draggedListBoxItem.Opacity = 1.0; } catch { }

        // Remove floating adorner if present
        try
        {
            if (_adornerLayer != null && _floatingAdorner != null)
            {
                _adornerLayer.Remove(_floatingAdorner);
            }
            _floatingAdorner = null;
        }
        catch { }

        // If user held Ctrl, treat this as a link operation instead of reorder
        if (_isLinkMode)
        {
            try
            {
                if (AssociatedObject.DataContext is Features.OrderLog.ViewModels.OrderLogViewModel vm)
                {
                    // resolve dragged order item from the DataContext (could be OrderItem or OrderItemGroup)
                    Features.OrderLog.Models.OrderItem? draggedOrder = null;
                    if (_draggedItem is Features.OrderLog.Models.OrderItem oi) draggedOrder = oi;
                    else if (_draggedItem is Features.OrderLog.ViewModels.OrderItemGroup grp) draggedOrder = grp.First;

                    if (draggedOrder != null)
                    {
                        var target = GetOrderItemAtPosition(_lastMousePos);
                        if (target != null)
                        {
                            var toLink = new System.Collections.Generic.List<Features.OrderLog.Models.OrderItem> { draggedOrder };
                            _ = vm.LinkItemsAsync(toLink, target);
                        }
                    }
                }
            }
            catch { }
        }
        else
        {
            // Move the item in the data source if position changed (insertion semantics)
            if (newIndex != oldIndex && AssociatedObject.ItemsSource is IList list)
            {
                var selectedItem = AssociatedObject.SelectedItem;
                var item = list[oldIndex];

                // Adjust insert index because removing the old item shifts indexes
                int insertIndex = newIndex;
                if (insertIndex > oldIndex)
                    insertIndex--;

                try {  } catch { }

                // Clamp insertIndex to [0, list.Count]
                insertIndex = Math.Max(0, Math.Min(insertIndex, list.Count));

                list.RemoveAt(oldIndex);
                list.Insert(insertIndex, item);
                AssociatedObject.SelectedItem = selectedItem;
                OnReorder?.Invoke();
            }
        }

        _isDragging = false;
        _draggedItem = null;
        _draggedIndex = -1;
        _dragStartIndex = -1;
        _draggedListBoxItem = null;
        _currentPreviewIndex = -1;
        HideAdorners();
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
        HideAdorners();
        _draggedItem = null;
        _draggedIndex = -1;
        _dragStartIndex = -1;
        // restore original item visibility
        try { if (_draggedListBoxItem != null) _draggedListBoxItem.Opacity = 1.0; } catch { }
        _draggedListBoxItem = null;
        _currentPreviewIndex = -1;
        HideAdorners();
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
            try
            {
                current = VisualTreeHelper.GetParent(current);
            }
            catch
            {
                try
                {
                    current = LogicalTreeHelper.GetParent(current);
                }
                catch
                {
                    current = null;
                }
            }
        }
        return null;
    }
}
