using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Behaviors;

/// <summary>
/// Fluid drag-and-drop behavior for OrderLog cards with smooth animations.
///
/// Features:
/// - iOS-like fluid card shifting during drag
/// - Ctrl+Drag to link items instead of reorder
/// - Multi-item drag support
/// - Hardware accelerated: GPU-rendered transforms, no layout invalidation
///
/// Usage in XAML:
/// <![CDATA[
/// <StackPanel>
///     <i:Interaction.Behaviors>
///         <behaviors:OrderLogFluidDragBehavior AnimationDuration="300"/>
///     </i:Interaction.Behaviors>
/// </StackPanel>
/// ]]>
///
/// Wire up events in code-behind:
/// <![CDATA[
/// behavior.ReorderComplete += async (items, target) => {
///     await viewModel.MoveOrdersAsync(items, target);
/// };
/// behavior.LinkComplete += async (items, target) => {
///     await viewModel.LinkItemsAsync(items, target);
/// };
/// ]]>
/// </remarks>
public class OrderLogFluidDragBehavior : Behavior<Panel>
{
    private Point _dragStartPoint;
    private Point _elementClickOffset; // Where on the element we clicked
    private Point _elementOriginalPosition; // Element's layout position before drag
    private bool _isDragging;
    private bool _isFinishingDrag; // Prevents re-entrant calls during finish
    private FrameworkElement? _draggedElement;
    private FrameworkElement? _draggedPanelChild;
    private FrameworkElement? _visualFeedbackBorder; // The border element that gets visual feedback (highlight)
    private List<OrderItem> _draggedItems = new();
    private int _draggedIndex = -1;
    private int _currentInsertionIndex = -1;
    private CardShiftAnimator? _animator;
    private bool _isLinkMode;
    private TransformGroup? _draggedTransform;
    private DateTime _lastShiftCalculation = DateTime.MinValue;
    private Brush? _originalBorderBrush;
    private Thickness _originalBorderThickness;

    private const int SHIFT_THROTTLE_MS = 16; // ~60fps
    private const double DRAG_SCALE = 1.02;
    private const int DRAG_Z_INDEX = 100;

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(
            nameof(AnimationDuration),
            typeof(double),
            typeof(OrderLogFluidDragBehavior),
            new PropertyMetadata(300.0));

    /// <summary>
    /// Animation duration in milliseconds (default: 300ms)
    /// </summary>
    public double AnimationDuration
    {
        get => (double)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>
    /// Event raised when a reorder operation completes.
    /// </summary>
    public event Action<List<OrderItem>, OrderItem?>? ReorderComplete;

    /// <summary>
    /// Event raised when a link operation completes.
    /// </summary>
    public event Action<List<OrderItem>, OrderItem?>? LinkComplete;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject == null) return;

        _animator = new CardShiftAnimator(
            AssociatedObject,
            TimeSpan.FromMilliseconds(AnimationDuration));

        AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        AssociatedObject.MouseLeave += OnMouseLeave;
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            AssociatedObject.MouseLeave -= OnMouseLeave;
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        }

        CancelDrag();
        base.OnDetaching();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging || _isFinishingDrag) return;

        // Check if the click is on a section drag handle - if so, ignore it
        // This allows the old DragDrop system to handle individual order drags from merged cards
        if (IsClickOnSectionDragHandle(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _dragStartPoint = e.GetPosition(AssociatedObject);

        // Find the dragged element (Border containing the card)
        _draggedElement = FindCardElement(e.OriginalSource as DependencyObject);

        if (_draggedElement != null)
        {
            // Determine the direct panel child (ContentControl) that contains this card.
            // Use a data-aware lookup so clicks inside subregions still map to the same
            // top-level child that represents the whole card/group.
            _draggedPanelChild = FindPanelChildForElement(_draggedElement);

            // Store where on the element we clicked. Prefer coordinates relative to the panel child
            // so dragging always moves the same visual (the panel child), regardless of click target.
            var clickTarget = _draggedPanelChild ?? _draggedElement;
            _elementClickOffset = e.GetPosition(clickTarget);

            _draggedItems = ExtractOrderItems(_draggedElement);
            _draggedIndex = GetElementIndex(_draggedElement);
        }
    }

    private FrameworkElement? FindPanelChildForElement(FrameworkElement element)
    {
        if (AssociatedObject == null || element == null) return null;

        var elementData = element.DataContext;

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null) continue;

            var bd = border.DataContext;

            // Direct reference match
            if (ReferenceEquals(bd, elementData)) return panelChild;

            // OrderItem identity
            if (bd is OrderItem bItem && elementData is OrderItem eItem)
            {
                if (bItem.Id == eItem.Id) return panelChild;
            }

            // Group identity - match by linked group id or first member id
            if (bd is OrderItemGroup bGroup && elementData is OrderItemGroup eGroup)
            {
                if (bGroup.LinkedGroupId != null && eGroup.LinkedGroupId != null && bGroup.LinkedGroupId == eGroup.LinkedGroupId)
                    return panelChild;
                if (bGroup.Members.Count > 0 && eGroup.Members.Count > 0 && bGroup.First.Id == eGroup.First.Id)
                    return panelChild;
            }

            if (bd is OrderItem bItem2 && elementData is OrderItemGroup eg2)
            {
                if (eg2.Members.Any(m => m.Id == bItem2.Id)) return panelChild;
            }

            if (bd is OrderItemGroup bg2 && elementData is OrderItem ei2)
            {
                if (bg2.Members.Any(m => m.Id == ei2.Id)) return panelChild;
            }
        }

        // Fallback to the simple parent lookup
        return FindPanelChild(element);
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (_isDragging)
            {
                FinishDrag();
            }
            return;
        }

        var currentPosition = e.GetPosition(AssociatedObject);

        if (!_isDragging)
        {
            // Check if we've exceeded the minimum drag distance
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                StartDrag();
            }
        }
        else
        {
            UpdateDrag(currentPosition);
        }
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            FinishDrag();
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        // Don't cancel drag on mouse leave - mouse capture keeps it going
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isDragging)
        {
            CancelDrag();
        }
    }

    private void StartDrag()
    {
        if (_draggedElement == null || AssociatedObject == null) return;

        _isDragging = true;
        _currentInsertionIndex = _draggedIndex;
        _isLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        // Ensure we have the panel child reference
        _draggedPanelChild ??= FindPanelChild(_draggedElement);

        // Use panel child as transform target for consistent movement, fallback to element
        var transformTarget = _draggedPanelChild ?? _draggedElement;
        _elementOriginalPosition = transformTarget.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));

        // Capture mouse and apply transforms
        Mouse.Capture(AssociatedObject, CaptureMode.SubTree);
        ApplyDragTransform(transformTarget);

        // Bring card to front
        if (_draggedPanelChild != null)
        {
            Panel.SetZIndex(_draggedPanelChild, DRAG_Z_INDEX);
        }

        // Apply visual feedback to the outermost border (entire card, not clicked sub-element)
        _visualFeedbackBorder = _draggedPanelChild != null
            ? FindVisualChildOfType<Border>(_draggedPanelChild)
            : _draggedElement;

        if (_visualFeedbackBorder != null)
        {
            ApplyModeVisualFeedback(_visualFeedbackBorder, _isLinkMode);
        }
    }

    private void UpdateDrag(Point currentPosition)
    {
        if (_draggedElement == null || _animator == null || AssociatedObject == null || _isFinishingDrag) return;

        // Always update dragged element position (no throttle for responsiveness)
        UpdateDraggedElementPosition(currentPosition);

        // Check for mode change (Ctrl key pressed/released)
        bool currentLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (currentLinkMode != _isLinkMode)
        {
            _isLinkMode = currentLinkMode;
            if (_visualFeedbackBorder != null)
            {
                ApplyModeVisualFeedback(_visualFeedbackBorder, _isLinkMode);
            }

            // When switching to link mode, clear any existing card shifts
            if (_isLinkMode)
            {
                _animator?.ClearTransforms();
            }
        }

        // Skip card shifting in link mode - we only shift cards when reordering
        if (_isLinkMode)
        {
            // For link mode, don't use insertion index - just track mouse position
            // We'll find the card directly under cursor when finishing drag
            return;
        }

        // Throttle expensive shift calculations to 60fps
        if ((DateTime.Now - _lastShiftCalculation).TotalMilliseconds < SHIFT_THROTTLE_MS)
            return;

        _lastShiftCalculation = DateTime.Now;

        // Calculate new insertion index
        int newInsertionIndex = _animator.CalculateInsertionIndex(currentPosition, _draggedElement, out _);

        // If insertion index changed, animate card shift
        if (newInsertionIndex != _currentInsertionIndex)
        {
            _currentInsertionIndex = newInsertionIndex;
            _animator.AnimateCardShift(_currentInsertionIndex, _draggedIndex, _draggedElement);
        }
    }

    private async void FinishDrag()
    {
        if (!_isDragging || _draggedElement == null || AssociatedObject == null || _isFinishingDrag)
        {
            CleanupDragState();
            return;
        }

        _isFinishingDrag = true; // Prevent re-entrant calls
        _isDragging = false; // Stop drag immediately to prevent mouse events

        // Release mouse capture
        Mouse.Capture(null);

        // Get target item before invoking events
        var targetItem = GetTargetItem();
        var draggedItems = new List<OrderItem>(_draggedItems);
        var isLinkMode = _isLinkMode; // Track if we are in link mode

        // Prefer to raise events so host views (widget, main view code-behind) can handle the
        // reorder/link logic. If no handlers are attached, fall back to calling the ViewModel directly.
        var viewModel = FindViewModel();
        try
        {
            if (isLinkMode)
            {
                var linkTarget = targetItem ?? FindNearestLinkTarget();

                if (LinkComplete != null)
                {
                    LinkComplete.Invoke(draggedItems, linkTarget);
                }
                else if (viewModel != null && linkTarget != null)
                {
                    await viewModel.LinkItemsAsync(draggedItems, linkTarget);
                }
                else if (viewModel != null)
                {
                    // No link target - fall back to move
                    await viewModel.MoveOrdersAsync(draggedItems, targetItem);
                }
            }
            else
            {
                if (ReorderComplete != null)
                {
                    ReorderComplete.Invoke(draggedItems, targetItem);
                }
                else if (viewModel != null)
                {
                    await viewModel.MoveOrdersAsync(draggedItems, targetItem);
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Exception in drag operation!\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                "Drag Error");
        }

        // Delay cleanup to allow ItemsControl to regenerate with new DisplayItems
        // ItemsControl needs time to rebuild its visual tree after ObservableCollection changes
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            CleanupDragState();
            _isFinishingDrag = false;
        };
        timer.Start();
    }

    private ViewModels.OrderLogViewModel? FindViewModel()
    {
        // Start from the panel and traverse up the visual tree
        DependencyObject? current = AssociatedObject;

        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is ViewModels.OrderLogViewModel vm)
            {
                return vm;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void CancelDrag()
    {
        if (!_isDragging) return;

        Mouse.Capture(null);

        // Animate back to original position
        var translateTransform = GetTranslateTransform();
        if (translateTransform != null)
        {
            var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(AnimationDuration))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, animation);
        }

        _animator?.ResetAllCardPositions();

        // Delay cleanup to allow animation to complete
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AnimationDuration + 50)
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            CleanupDragState();
        };
        timer.Start();
    }

    /// <summary>
    /// Gets the TranslateTransform from the current drag transform group.
    /// </summary>
    private TranslateTransform? GetTranslateTransform()
    {
        return _draggedTransform?.Children.OfType<TranslateTransform>().FirstOrDefault();
    }

    private void CleanupDragState()
    {
        // Clear transforms
        var transformTarget = _draggedPanelChild ?? _draggedElement;
        if (transformTarget != null)
        {
            transformTarget.RenderTransform = null;
        }

        // Reset Z-Index
        if (_draggedPanelChild != null)
        {
            Panel.SetZIndex(_draggedPanelChild, 0);
        }

        // Restore visual feedback border
        if (_visualFeedbackBorder is Border border)
        {
            border.BorderBrush = _originalBorderBrush;
            border.BorderThickness = _originalBorderThickness;
            border.Effect = null;
        }

        // Clear animator transforms
        _animator?.ClearTransforms();

        // Reset all state variables
        _isDragging = false;
        _draggedElement = null;
        _draggedPanelChild = null;
        _visualFeedbackBorder = null;
        _draggedItems.Clear();
        _draggedIndex = -1;
        _currentInsertionIndex = -1;
        _draggedTransform = null;
        _originalBorderBrush = null;
    }

    private void ApplyDragTransform(FrameworkElement element)
    {
        // Create transform group with scale and translate
        _draggedTransform = new TransformGroup();

        var scaleTransform = new ScaleTransform(DRAG_SCALE, DRAG_SCALE)
        {
            CenterX = element.ActualWidth / 2,
            CenterY = element.ActualHeight / 2
        };

        var translateTransform = new TranslateTransform(0, 0);

        _draggedTransform.Children.Add(scaleTransform);
        _draggedTransform.Children.Add(translateTransform);

        element.RenderTransform = _draggedTransform;
    }

    private void UpdateDraggedElementPosition(Point currentPosition)
    {
        if (_draggedElement == null || AssociatedObject == null || _isFinishingDrag) return;

        var translateTransform = GetTranslateTransform();
        if (translateTransform == null) return;

        // Calculate where the element should be positioned so the click point follows the mouse
        // desiredPosition = mousePosition - clickOffset
        var desiredX = currentPosition.X - _elementClickOffset.X;
        var desiredY = currentPosition.Y - _elementClickOffset.Y;

        // Get panel bounds to constrain the drag
        var panelBounds = new Rect(0, 0, AssociatedObject.ActualWidth, AssociatedObject.ActualHeight);

        // Constrain position to keep card visible horizontally and prevent going above panel
        // Allow dragging below panel bounds so tall cards can reach the bottom insertion point
        const double margin = 10;
        var sizeTarget = _draggedPanelChild ?? _draggedElement!;
        desiredX = Math.Max(margin, Math.Min(desiredX, panelBounds.Width - sizeTarget.ActualWidth - margin));
        desiredY = Math.Max(margin, desiredY); // Only constrain top, allow dragging below panel

        // Calculate the offset from the element's original layout position (stored at drag start)
        translateTransform.X = desiredX - _elementOriginalPosition.X;
        translateTransform.Y = desiredY - _elementOriginalPosition.Y;
    }

    private void ApplyModeVisualFeedback(FrameworkElement element, bool isLinkMode)
    {
        if (element is not Border border) return;

        // Store original if not already stored
        if (_originalBorderBrush == null)
        {
            _originalBorderBrush = border.BorderBrush;
            _originalBorderThickness = border.BorderThickness;
        }

        // Determine target color based on mode
        var targetColor = isLinkMode
            ? Color.FromRgb(138, 43, 226) // Purple for link mode
            : Color.FromRgb(34, 197, 94); // Green for reorder (from SuccessBrush)

        var targetThickness = new Thickness(4); // Slightly thicker for more visibility

        // Create a new mutable brush for animation (frozen brushes can't be animated)
        var newBrush = new SolidColorBrush(border.BorderBrush is SolidColorBrush currentBrush
            ? currentBrush.Color
            : Colors.Gray);

        border.BorderBrush = newBrush;
        border.BorderThickness = targetThickness;

        // Add glow effect for link mode
        if (isLinkMode)
        {
            var dropShadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(138, 43, 226), // Purple glow
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.8
            };
            border.Effect = dropShadow;
        }
        else
        {
            // Remove glow effect for reorder mode
            border.Effect = null;
        }

        // Animate to target color
        var colorAnimation = new ColorAnimation
        {
            To = targetColor,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        newBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
    }

    private FrameworkElement? FindCardElement(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            // Check if this is the Border with OrderItem/OrderItemGroup DataContext
            if (current is Border border &&
                (border.DataContext is OrderItem || border.DataContext is OrderItemGroup))
            {
                return border;
            }

            // Check ContentControl and search its visual children
            if (current is ContentControl control &&
                (control.DataContext is OrderItem || control.DataContext is OrderItemGroup))
            {
                // Search visual children for Border
                var childBorder = FindVisualChildOfType<Border>(control);
                if (childBorder != null)
                    return childBorder;
            }

            // Stop if we've reached the panel (don't go beyond the ItemsControl)
            if (current == AssociatedObject)
                break;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindVisualChildOfType<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChildOfType<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    private bool IsClickOnSectionDragHandle(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            // Check if this is a Border with a drag handle tooltip
            if (current is Border border)
            {
                var tooltip = ToolTipService.GetToolTip(border);
                if (tooltip is string tooltipText &&
                    tooltipText.Contains("Drag to move this order separately", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Stop if we've reached the panel
            if (current == AssociatedObject)
                break;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private FrameworkElement? FindPanelChild(DependencyObject? element)
    {
        if (element == null || AssociatedObject == null) return null;

        var current = element;
        while (current != null)
        {
            var parent = VisualTreeHelper.GetParent(current);

            // If parent is the panel, then current is the direct child we want
            if (parent == AssociatedObject && current is FrameworkElement fe)
            {
                return fe;
            }

            current = parent;
        }

        return null;
    }

    private List<OrderItem> ExtractOrderItems(FrameworkElement element)
    {
        if (element.DataContext is OrderItem item)
        {
            return new List<OrderItem> { item };
        }

        if (element.DataContext is OrderItemGroup group)
        {
            return group.Members.ToList();
        }

        return new List<OrderItem>();
    }

    private int GetElementIndex(FrameworkElement element)
    {
        if (AssociatedObject == null) return -1;

        if (element == null) return -1;

        // Build the list of visible card Border elements (same criteria as GetTargetItem)
        var children = new List<FrameworkElement>();
        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border != null && (border.DataContext is OrderItem || border.DataContext is OrderItemGroup))
            {
                children.Add(border);
            }
        }

        // Try to find a matching index by reference first, then by logical identity (IDs/group)
        for (int i = 0; i < children.Count; i++)
        {
            var b = children[i];
            if (ReferenceEquals(b, element))
                return i;

            var bdc = b.DataContext;
            var edc = element.DataContext;

            if (bdc is OrderItem bItem && edc is OrderItem eItem)
            {
                if (bItem.Id == eItem.Id) return i;
            }
            else if (bdc is OrderItemGroup bGroup && edc is OrderItemGroup eGroup)
            {
                // Compare by first member id or linked group id when available
                if (bGroup.Members.Count > 0 && eGroup.Members.Count > 0 && bGroup.First.Id == eGroup.First.Id)
                    return i;
                if (bGroup.LinkedGroupId != null && eGroup.LinkedGroupId != null && bGroup.LinkedGroupId == eGroup.LinkedGroupId)
                    return i;
            }
            else if (bdc is OrderItem bItem2 && edc is OrderItemGroup eGroup2)
            {
                if (eGroup2.Members.Any(m => m.Id == bItem2.Id)) return i;
            }
            else if (bdc is OrderItemGroup bGroup2 && edc is OrderItem eItem2)
            {
                if (bGroup2.Members.Any(m => m.Id == eItem2.Id)) return i;
            }
        }

        return -1;
    }

    private OrderItem? GetTargetItem()
    {
        if (AssociatedObject == null) return null;

        // In link mode, find the card directly under the mouse cursor
        if (_isLinkMode)
        {
            return FindCardUnderCursor();
        }

        // In reorder mode, use insertion index
        if (_currentInsertionIndex < 0 || _animator == null)
            return null;

        // Get all Border elements EXCEPT the dragged one
        var children = new List<FrameworkElement>();
        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border != null &&
                border != _draggedElement &&  // Exclude dragged element
                (border.DataContext is OrderItem || border.DataContext is OrderItemGroup))
            {
                children.Add(border);
            }
        }

        // Adjust insertion index since we excluded the dragged element
        // If dragging forward (insertion index > dragged index), subtract 1
        int adjustedIndex = _currentInsertionIndex;
        if (_currentInsertionIndex > _draggedIndex)
        {
            adjustedIndex = _currentInsertionIndex - 1;
        }

        // Target is the item at the adjusted position
        if (adjustedIndex >= children.Count)
        {
            // Inserting at the end - target is null (append)
            return null;
        }

        var targetElement = children[adjustedIndex];

        if (targetElement.DataContext is OrderItem item)
            return item;

        if (targetElement.DataContext is OrderItemGroup group)
            return group.First;

        return null;
    }

    private OrderItem? FindCardUnderCursor()
    {
        if (AssociatedObject == null || _draggedElement == null) return null;

        // Get current mouse position relative to the panel
        var mousePosition = Mouse.GetPosition(AssociatedObject);

        // Find all Border elements EXCEPT the dragged one
        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null || border == _draggedElement)
                continue;

            if (!(border.DataContext is OrderItem || border.DataContext is OrderItemGroup))
                continue;

            // Check if mouse is over this border
            var borderBounds = new Rect(
                border.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0)),
                new Size(border.ActualWidth, border.ActualHeight)
            );

            if (borderBounds.Contains(mousePosition))
            {
                if (border.DataContext is OrderItem item)
                    return item;

                if (border.DataContext is OrderItemGroup group)
                    return group.First;
            }
        }

        return null; // No card under cursor
    }

    private OrderItem? FindNearestLinkTarget()
    {
        if (AssociatedObject == null || _draggedElement == null) return null;

        // Get all Border elements EXCEPT the one being dragged
        var children = new List<FrameworkElement>();
        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border != null &&
                border != _draggedElement &&  // Exclude the dragged element
                (border.DataContext is OrderItem || border.DataContext is OrderItemGroup))
            {
                children.Add(border);
            }
        }

        if (children.Count == 0) return null;

        // When dropping at the end in link mode, link with the last item
        // This handles cases like dragging a 3rd order to link with an existing group of 2
        var targetElement = children[children.Count - 1];

        if (targetElement.DataContext is OrderItem item)
            return item;

        if (targetElement.DataContext is OrderItemGroup group)
            return group.First;

        return null;
    }
}
