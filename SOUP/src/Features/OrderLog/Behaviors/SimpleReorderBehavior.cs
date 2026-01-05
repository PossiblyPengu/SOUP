using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Behaviors;

/// <summary>
/// Simple drag-to-reorder behavior that:
/// 1. Uses an adorner for the drag preview (no transforms on actual items)
/// 2. Shows an insertion indicator line for reorder mode
/// 3. Supports Ctrl+drag to link orders together
/// 4. Only modifies the collection on drop (no stuttering)
/// </summary>
public class SimpleReorderBehavior : Behavior<Panel>
{
    private const int DRAG_THRESHOLD = 5;
    
    // Drag state
    private bool _isDragging;
    private bool _isLinkMode;
    private Point _dragStartPoint;
    private OrderItem? _draggedItem;
    private FrameworkElement? _draggedElement;
    private int _draggedIndex;
    private int _targetIndex;
    private OrderItem? _linkTarget;
    private FrameworkElement? _linkTargetElement;
    
    // Visual elements
    private DragAdorner? _dragAdorner;
    private InsertionIndicatorAdorner? _insertionAdorner;
    private AdornerLayer? _adornerLayer;
    
    // Original styles for link target highlight
    private Brush? _originalTargetBorderBrush;
    private Thickness _originalTargetBorderThickness;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
        AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseUp;
        AssociatedObject.MouseLeave += OnMouseLeave;
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        AssociatedObject.PreviewKeyUp += OnPreviewKeyUp;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
        AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseUp;
        AssociatedObject.MouseLeave -= OnMouseLeave;
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        AssociatedObject.PreviewKeyUp -= OnPreviewKeyUp;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isDragging && e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            SetLinkMode(true);
        }
        else if (e.Key == Key.Escape && _isDragging)
        {
            CancelDrag();
            Reset();
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_isDragging && (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl))
        {
            SetLinkMode(false);
        }
    }

    private void SetLinkMode(bool enabled)
    {
        if (_isLinkMode == enabled) return;
        _isLinkMode = enabled;
        
        // Update adorner color based on mode
        _dragAdorner?.SetLinkMode(enabled);
        
        if (enabled)
        {
            // Hide insertion indicator in link mode
            _insertionAdorner?.Hide();
        }
        else
        {
            // Clear link target highlight
            ClearLinkTargetHighlight();
            _linkTarget = null;
            _linkTargetElement = null;
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        
        // Don't start drag if clicking on editable controls
        var originalElement = e.OriginalSource as DependencyObject;
        if (IsEditableControl(originalElement))
        {
            return;
        }
        
        _dragStartPoint = e.GetPosition(AssociatedObject);
        
        // Find the item being clicked
        var element = originalElement;
        while (element != null && element != AssociatedObject)
        {
            if (element is FrameworkElement fe)
            {
                var item = GetOrderItem(fe.DataContext);
                if (item != null)
                {
                    _draggedItem = item;
                    _draggedElement = FindItemContainer(fe);
                    _draggedIndex = GetItemIndex(item);
                    _targetIndex = _draggedIndex;
                    return;
                }
            }
            element = VisualTreeHelper.GetParent(element);
        }
    }
    
    /// <summary>
    /// Checks if the element or any of its parents is an editable control (TextBox, RichTextBox, ComboBox)
    /// Also checks for FlowDocument elements (Run, Paragraph, etc.) which are inside RichTextBox
    /// </summary>
    private static bool IsEditableControl(DependencyObject? element)
    {
        // First check if it's a FlowDocument element (inside RichTextBox)
        // These are in the logical tree, not visual tree
        if (element is System.Windows.Documents.TextElement ||
            element is System.Windows.Documents.FlowDocument)
        {
            return true;
        }
        
        // Walk up visual tree to find editable controls
        while (element != null)
        {
            if (element is System.Windows.Controls.TextBox ||
                element is System.Windows.Controls.RichTextBox ||
                element is System.Windows.Controls.ComboBox ||
                element is System.Windows.Controls.Primitives.TextBoxBase ||
                element is System.Windows.Documents.FlowDocument)
            {
                return true;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedItem == null || e.LeftButton != MouseButtonState.Pressed) return;

        var currentPos = e.GetPosition(AssociatedObject);
        var diff = currentPos - _dragStartPoint;

        // Check if we've moved enough to start dragging
        if (!_isDragging && (Math.Abs(diff.X) > DRAG_THRESHOLD || Math.Abs(diff.Y) > DRAG_THRESHOLD))
        {
            StartDrag();
        }

        if (_isDragging)
        {
            UpdateDrag(currentPos);
        }
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            FinishDrag();
        }
        Reset();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            CancelDrag();
        }
        Reset();
    }

    private void StartDrag()
    {
        if (_draggedElement == null || AssociatedObject == null) return;

        _isDragging = true;
        _isLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        Mouse.Capture(AssociatedObject);

        // Create adorner layer
        _adornerLayer = AdornerLayer.GetAdornerLayer(AssociatedObject);
        if (_adornerLayer == null) return;

        // Create drag adorner (visual copy of the item)
        _dragAdorner = new DragAdorner(AssociatedObject, _draggedElement, _dragStartPoint, _isLinkMode);
        _adornerLayer.Add(_dragAdorner);

        // Create insertion indicator (only visible in reorder mode)
        _insertionAdorner = new InsertionIndicatorAdorner(AssociatedObject);
        _adornerLayer.Add(_insertionAdorner);
        
        if (_isLinkMode)
        {
            _insertionAdorner.Hide();
        }
    }

    private void UpdateDrag(Point mousePosition)
    {
        if (!_isDragging || _adornerLayer == null) return;

        // Check for mode change
        bool wantLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (wantLinkMode != _isLinkMode)
        {
            SetLinkMode(wantLinkMode);
        }

        // Update drag adorner position
        _dragAdorner?.UpdatePosition(mousePosition);

        if (_isLinkMode)
        {
            // Link mode: find and highlight target under cursor
            UpdateLinkTarget(mousePosition);
        }
        else
        {
            // Reorder mode: calculate target insertion index
            _targetIndex = CalculateTargetIndex(mousePosition);
            UpdateInsertionIndicator(_targetIndex);
        }
    }

    private void UpdateLinkTarget(Point mousePosition)
    {
        // Find item under cursor (excluding dragged item)
        OrderItem? newTarget = null;
        FrameworkElement? newTargetElement = null;

        foreach (var (item, element) in GetOrderedItems())
        {
            if (item == _draggedItem || element == null) continue;

            var pos = element.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
            var bounds = new Rect(pos, new Size(element.ActualWidth, element.ActualHeight));

            if (bounds.Contains(mousePosition))
            {
                newTarget = item;
                newTargetElement = element;
                break;
            }
        }

        // Update highlight if target changed
        if (newTarget != _linkTarget)
        {
            ClearLinkTargetHighlight();
            _linkTarget = newTarget;
            _linkTargetElement = newTargetElement;
            ApplyLinkTargetHighlight();
        }
    }

    private void ApplyLinkTargetHighlight()
    {
        if (_linkTargetElement == null) return;

        // Find the border in the element
        var border = FindVisualChild<Border>(_linkTargetElement);
        if (border != null)
        {
            _originalTargetBorderBrush = border.BorderBrush;
            _originalTargetBorderThickness = border.BorderThickness;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
            border.BorderThickness = new Thickness(2);
        }
    }

    private void ClearLinkTargetHighlight()
    {
        if (_linkTargetElement == null) return;

        var border = FindVisualChild<Border>(_linkTargetElement);
        if (border != null)
        {
            border.BorderBrush = _originalTargetBorderBrush;
            border.BorderThickness = _originalTargetBorderThickness;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private int CalculateTargetIndex(Point mousePosition)
    {
        var children = GetOrderedItems().ToList();
        if (children.Count == 0) return 0;

        double mouseY = mousePosition.Y;
        
        for (int i = 0; i < children.Count; i++)
        {
            var (item, element) = children[i];
            if (element == null) continue;
            
            var pos = element.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
            double itemMidpoint = pos.Y + element.ActualHeight / 2;

            if (mouseY < itemMidpoint)
            {
                return i;
            }
        }

        return children.Count; // After the last item
    }

    private void UpdateInsertionIndicator(int targetIndex)
    {
        if (_insertionAdorner == null || AssociatedObject == null) return;

        var children = GetOrderedItems().ToList();
        double indicatorY = 0;

        if (targetIndex <= 0)
        {
            // Before first item
            if (children.Count > 0 && children[0].element != null)
            {
                var pos = children[0].element!.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
                indicatorY = pos.Y;
            }
        }
        else if (targetIndex >= children.Count)
        {
            // After last item
            if (children.Count > 0 && children[^1].element != null)
            {
                var lastElement = children[^1].element!;
                var pos = lastElement.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
                indicatorY = pos.Y + lastElement.ActualHeight;
            }
        }
        else
        {
            // Between items
            if (children[targetIndex].element != null)
            {
                var pos = children[targetIndex].element!.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
                indicatorY = pos.Y;
            }
        }

        _insertionAdorner.UpdatePosition(indicatorY, AssociatedObject.ActualWidth);
    }

    private async void FinishDrag()
    {
        if (_draggedItem == null) return;

        var viewModel = FindViewModel();
        if (viewModel == null)
        {
            CleanupAdorners();
            return;
        }

        if (_isLinkMode && _linkTarget != null)
        {
            // Link mode: link the dragged item to the target
            await viewModel.LinkItemsAsync(new List<OrderItem> { _draggedItem }, _linkTarget);
        }
        else
        {
            // Reorder mode: move item to new position
            int finalIndex = _targetIndex;
            if (finalIndex > _draggedIndex)
            {
                finalIndex--; // Account for the item being removed first
            }

            if (finalIndex != _draggedIndex && finalIndex >= 0)
            {
                viewModel.MoveItemToIndex(_draggedItem, finalIndex);
                await viewModel.SaveAsync();
            }
        }

        CleanupAdorners();
    }

    private void CancelDrag()
    {
        CleanupAdorners();
    }

    private void CleanupAdorners()
    {
        // Clear link target highlight
        ClearLinkTargetHighlight();

        // Remove adorners
        if (_adornerLayer != null)
        {
            if (_dragAdorner != null)
            {
                _adornerLayer.Remove(_dragAdorner);
                _dragAdorner = null;
            }
            if (_insertionAdorner != null)
            {
                _adornerLayer.Remove(_insertionAdorner);
                _insertionAdorner = null;
            }
        }

        Mouse.Capture(null);
    }

    private void Reset()
    {
        _isDragging = false;
        _isLinkMode = false;
        _draggedItem = null;
        _draggedElement = null;
        _draggedIndex = -1;
        _targetIndex = -1;
        _linkTarget = null;
        _linkTargetElement = null;
    }

    private IEnumerable<(OrderItem item, FrameworkElement? element)> GetOrderedItems()
    {
        if (AssociatedObject == null) yield break;

        foreach (var child in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (child.Visibility != Visibility.Visible) continue;

            var item = GetOrderItem(child.DataContext);
            if (item != null)
            {
                yield return (item, child);
            }
        }
    }

    private OrderItem? GetOrderItem(object? dataContext)
    {
        if (dataContext is OrderItem item) return item;
        if (dataContext is OrderItemGroup group) return group.First;
        return null;
    }

    private int GetItemIndex(OrderItem item)
    {
        var viewModel = FindViewModel();
        if (viewModel == null) return -1;
        return viewModel.Items.IndexOf(item);
    }

    private FrameworkElement? FindItemContainer(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current != null && current != AssociatedObject)
        {
            if (current is FrameworkElement fe && VisualTreeHelper.GetParent(fe) == AssociatedObject)
            {
                return fe;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return element;
    }

    private OrderLogViewModel? FindViewModel()
    {
        DependencyObject? current = AssociatedObject;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is OrderLogViewModel vm)
            {
                return vm;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

/// <summary>
/// Adorner that displays a semi-transparent copy of the dragged item.
/// </summary>
public class DragAdorner : Adorner
{
    private readonly VisualBrush _visualBrush;
    private readonly Size _size;
    private Point _offset;
    private Point _currentPosition;
    private bool _isLinkMode;
    private Color _borderColor = Color.FromRgb(99, 102, 241); // Indigo for reorder

    public DragAdorner(UIElement adornedElement, FrameworkElement draggedElement, Point startPoint, bool isLinkMode = false) 
        : base(adornedElement)
    {
        _size = new Size(draggedElement.ActualWidth, draggedElement.ActualHeight);
        _visualBrush = new VisualBrush(draggedElement)
        {
            Opacity = 0.8,
            Stretch = Stretch.None
        };
        _isLinkMode = isLinkMode;
        _borderColor = isLinkMode ? Color.FromRgb(34, 197, 94) : Color.FromRgb(99, 102, 241);

        // Calculate offset from mouse to element top-left
        var elementPos = draggedElement.TransformToAncestor(adornedElement).Transform(new Point(0, 0));
        _offset = new Point(startPoint.X - elementPos.X, startPoint.Y - elementPos.Y);
        _currentPosition = startPoint;

        IsHitTestVisible = false;
    }

    public void SetLinkMode(bool isLinkMode)
    {
        _isLinkMode = isLinkMode;
        _borderColor = isLinkMode ? Color.FromRgb(34, 197, 94) : Color.FromRgb(99, 102, 241);
        InvalidateVisual();
    }

    public void UpdatePosition(Point mousePosition)
    {
        _currentPosition = mousePosition;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(
            _currentPosition.X - _offset.X,
            _currentPosition.Y - _offset.Y,
            _size.Width,
            _size.Height
        );

        // Draw drop shadow
        var shadowRect = rect;
        shadowRect.Offset(3, 3);
        drawingContext.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
            null,
            shadowRect
        );

        // Draw the element
        drawingContext.DrawRectangle(_visualBrush, null, rect);
        
        // Draw border to indicate mode
        var pen = new Pen(new SolidColorBrush(_borderColor), 2);
        drawingContext.DrawRectangle(null, pen, rect);
    }
}

/// <summary>
/// Adorner that displays a horizontal line indicating where the item will be inserted.
/// </summary>
public class InsertionIndicatorAdorner : Adorner
{
    private double _y;
    private double _width;
    private bool _isVisible = true;
    private readonly Pen _pen;

    public InsertionIndicatorAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _pen = new Pen(new SolidColorBrush(Color.FromRgb(99, 102, 241)), 3) // Indigo color
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
    }

    public void Hide()
    {
        _isVisible = false;
        InvalidateVisual();
    }

    public void Show()
    {
        _isVisible = true;
        InvalidateVisual();
    }

    public void UpdatePosition(double y, double width)
    {
        _y = y;
        _width = width;
        _isVisible = true;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!_isVisible || _width <= 0) return;

        // Draw horizontal line
        drawingContext.DrawLine(_pen, new Point(8, _y), new Point(_width - 8, _y));

        // Draw small circles at the ends
        var circleBrush = _pen.Brush;
        drawingContext.DrawEllipse(circleBrush, null, new Point(8, _y), 4, 4);
        drawingContext.DrawEllipse(circleBrush, null, new Point(_width - 8, _y), 4, 4);
    }
}
