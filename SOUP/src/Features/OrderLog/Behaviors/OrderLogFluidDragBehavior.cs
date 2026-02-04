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
/// <para>
/// Features:
/// - iOS-like fluid card shifting during drag
/// - Ctrl+Drag to link items instead of reorder
/// - Multi-item drag support
/// - Hardware accelerated: GPU-rendered transforms, no layout invalidation
/// </para>
/// </summary>
/// <remarks>
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
    private int _currentLogicalIndex = -1; // Track where the dragged card logically is after swaps
    private CardShiftAnimator? _animator;
    private double? _lockedPanelWidth;
    private bool _isLinkMode;
    private bool _initialClickWasOnHandle;
    private TransformGroup? _draggedTransform;
    private FrameworkElement? _currentLinkTargetBorder; // Visual feedback for the card we will link to
    private DateTime _lastShiftCalculation = DateTime.MinValue;
    private Brush? _originalBorderBrush;
    private Thickness _originalBorderThickness;
    private System.Windows.Threading.DispatcherTimer? _cleanupTimer;

    private const int SHIFT_THROTTLE_MS = 16; // ~60fps
    private const double DRAG_SCALE = 1.05; // Slightly larger for better visibility
    private const int DRAG_Z_INDEX = 100;
    private const double DRAG_SHADOW_BLUR = 30;
    private const double DRAG_SHADOW_OPACITY = 0.5;

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(
            nameof(AnimationDuration),
            typeof(double),
            typeof(OrderLogFluidDragBehavior),
            new PropertyMetadata(200.0)); // Faster, snappier animations

    public static readonly DependencyProperty RequireDragHandleProperty =
        DependencyProperty.Register(
            nameof(RequireDragHandle),
            typeof(bool),
            typeof(OrderLogFluidDragBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty EnableReorderingProperty =
        DependencyProperty.Register(
            nameof(EnableReordering),
            typeof(bool),
            typeof(OrderLogFluidDragBehavior),
            new PropertyMetadata(true));

    /// <summary>
    /// Animation duration in milliseconds (default: 300ms)
    /// </summary>
    public double AnimationDuration
    {
        get => (double)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>
    /// When true, drag can only be initiated from elements with Tag="DragHandle".
    /// When false (default), drag can be initiated from anywhere on the card.
    /// </summary>
    public bool RequireDragHandle
    {
        get => (bool)GetValue(RequireDragHandleProperty);
        set => SetValue(RequireDragHandleProperty, value);
    }

    /// <summary>
    /// When false, reordering is disabled and only link-mode (Ctrl+Drag) is allowed.
    /// </summary>
    public bool EnableReordering
    {
        get => (bool)GetValue(EnableReorderingProperty);
        set => SetValue(EnableReorderingProperty, value);
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

        // Don't interfere with editable controls - let them handle their own mouse events
        if (IsClickOnEditableControl(e.OriginalSource as DependencyObject))
        {
            _draggedElement = null;
            _draggedPanelChild = null;
            return;
        }

        // Check if the click is on a section drag handle.
        // Historically we returned here to let the old DragDrop system handle
        // individual member drags from merged/grouped cards. To prevent
        // accidental reordering we only fall back to the old path when the
        // user is intentionally holding Control (Ctrl+drag). Otherwise let
        // our behavior handle it (StartDrag will block if Ctrl isn't held).
        if (IsClickOnSectionDragHandle(e.OriginalSource as DependencyObject))
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                return; // allow legacy drag behaviour when Ctrl is down
            }
            // otherwise continue to let this behavior decide (it will require Ctrl to actually start)
        }

        // Record whether the initial click was on a drag handle so StartDrag
        // can decide whether Ctrl is required. Do not early-return here; let
        // StartDrag enforce gating so non-handle Ctrl+drag for linking still works.
        _initialClickWasOnHandle = IsClickOnDragHandle(e.OriginalSource as DependencyObject);

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
            _currentLogicalIndex = _draggedIndex; // Track logical position for swap-based reordering
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
                if (bGroup.Members?.Count > 0 && eGroup.Members?.Count > 0 && bGroup.First?.Id == eGroup.First?.Id)
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
        // Decide whether drag may start:
        // - If the initial click was on the visible handle, allow drag start regardless of Ctrl (for reorder).
        // - If not clicking the handle, only allow start when Control is held (user intends linking).
        if (!_initialClickWasOnHandle && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return; // not a handle click and Ctrl not held -> don't start
        }

        _isDragging = true;
        _currentLogicalIndex = _draggedIndex;
        _isLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        // Ensure we have the panel child reference
        _draggedPanelChild ??= FindPanelChild(_draggedElement);

        // Use panel child as transform target for consistent movement, fallback to element
        var transformTarget = _draggedPanelChild ?? _draggedElement;
        _elementOriginalPosition = transformTarget.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));

        // Capture mouse and apply transforms
        Mouse.Capture(AssociatedObject, CaptureMode.SubTree);
        // Lock the panel width to prevent wrapping changes while dragging
        _lockedPanelWidth = AssociatedObject.ActualWidth;
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
        if (_draggedElement == null || AssociatedObject == null || _isFinishingDrag) return;

        // Check for mode change (Ctrl key pressed/released)
        bool currentLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (currentLinkMode != _isLinkMode)
        {
            _isLinkMode = currentLinkMode;
            if (_visualFeedbackBorder != null)
            {
                ApplyModeVisualFeedback(_visualFeedbackBorder, _isLinkMode);
            }
        }

        // Link mode: move card with mouse and highlight target
        if (_isLinkMode)
        {
            UpdateDraggedElementPosition(currentPosition);

            var targetBorder = FindBorderUnderCursor();
            if (!ReferenceEquals(targetBorder, _currentLinkTargetBorder))
            {
                ClearTargetVisualFeedback();
                if (targetBorder != null)
                {
                    ApplyTargetVisualFeedback(targetBorder);
                }
                _currentLinkTargetBorder = targetBorder;
            }
            return;
        }

        // Reorder mode: move card with mouse and swap positions when crossing midpoints
        UpdateDraggedElementPosition(currentPosition);

        // Throttle swap calculations
        if ((DateTime.Now - _lastShiftCalculation).TotalMilliseconds < SHIFT_THROTTLE_MS)
            return;

        _lastShiftCalculation = DateTime.Now;

        PerformSwapBasedReorder(currentPosition);
    }

    /// <summary>
    /// Tracks which cards have been shifted during drag.
    /// </summary>
    private HashSet<int> _shiftedCards = new();

    /// <summary>
    /// Swap-based reordering: move items in the collection as the user drags.
    /// After moving, we re-acquire references to the new visual elements.
    /// </summary>
    private void PerformSwapBasedReorder(Point mousePosition)
    {
        if (_draggedElement == null || AssociatedObject == null) return;

        var viewModel = FindViewModel();
        if (viewModel == null) return;

        var draggedItem = _draggedItems.FirstOrDefault();
        if (draggedItem == null) return;

        // Get current mouse position relative to panel
        var mouseY = mousePosition.Y - _dragStartPoint.Y + _elementOriginalPosition.Y + (_draggedElement.ActualHeight / 2);

        var children = GetAllCardBorders();
        if (children.Count == 0) return;

        // Find the target index based on mouse Y position
        int targetIndex = _currentLogicalIndex;

        for (int i = 0; i < children.Count; i++)
        {
            var card = children[i];

            // Skip our own card
            var cardItem = GetOrderItemFromElement(card);
            if (cardItem != null && cardItem.Id == draggedItem.Id) continue;

            var pos = card.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
            double cardMidpoint = pos.Y + card.ActualHeight / 2;

            if (mouseY < cardMidpoint)
            {
                targetIndex = i;
                break;
            }
            else
            {
                targetIndex = i + 1;
            }
        }

        // Clamp to valid range
        targetIndex = Math.Max(0, Math.Min(targetIndex, children.Count - 1));

        // If target differs from current position, move the item
        if (targetIndex != _currentLogicalIndex)
        {
            // Store current transform offset before move
            var currentTransform = GetTranslateTransform();
            double currentOffsetY = currentTransform?.Y ?? 0;
            double currentOffsetX = currentTransform?.X ?? 0;

            // Move item in collection
            viewModel.MoveItemToIndex(draggedItem, targetIndex);
            _currentLogicalIndex = targetIndex;
            _draggedIndex = targetIndex;

            // Force layout update
            AssociatedObject.UpdateLayout();

            // Re-acquire the visual element for the dragged item
            ReacquireDraggedElement(draggedItem, currentOffsetX, currentOffsetY);
        }
    }

    private void ReacquireDraggedElement(OrderItem draggedItem, double offsetX, double offsetY)
    {
        if (AssociatedObject == null) return;

        // Find the new visual element for our item
        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null) continue;

            var item = GetOrderItemFromElement(border);
            if (item != null && item.Id == draggedItem.Id)
            {
                // Found it - update our references
                _draggedElement = border;
                _draggedPanelChild = panelChild;

                // Update original position to new location
                _elementOriginalPosition = panelChild.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));

                // Re-apply transform with adjusted offset
                ApplyDragTransform(panelChild);
                var newTransform = GetTranslateTransform();
                if (newTransform != null)
                {
                    newTransform.X = offsetX;
                    newTransform.Y = offsetY;
                }

                // Re-apply Z-index and visual feedback
                Panel.SetZIndex(panelChild, DRAG_Z_INDEX);

                _visualFeedbackBorder = border;
                ApplyModeVisualFeedback(border, _isLinkMode);

                return;
            }
        }
    }

    private OrderItem? GetOrderItemFromElement(FrameworkElement element)
    {
        if (element.DataContext is OrderItem item) return item;
        if (element.DataContext is OrderItemGroup group) return group.First;
        return null;
    }

    private Point GetCardOriginalPosition(FrameworkElement card, int visualIndex)
    {
        if (AssociatedObject == null) return new Point(0, 0);

        // Get current position and subtract any transform offset
        var pos = card.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));

        // Remove any animator transform to get original position
        var transform = card.RenderTransform as TranslateTransform;
        if (transform != null)
        {
            pos.Y -= transform.Y;
        }
        else if (card.RenderTransform is TransformGroup group)
        {
            var tt = group.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (tt != null) pos.Y -= tt.Y;
        }

        return pos;
    }

    private Point GetDraggedCardCenter()
    {
        if (_draggedElement == null || AssociatedObject == null)
            return new Point(0, 0);

        var pos = _draggedElement.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
        return new Point(
            pos.X + _draggedElement.ActualWidth / 2,
            pos.Y + _draggedElement.ActualHeight / 2
        );
    }

    private List<FrameworkElement> GetAllCardBorders()
    {
        var result = new List<FrameworkElement>();
        if (AssociatedObject == null) return result;

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border != null && IsRenderableDataContext(border.DataContext))
            {
                result.Add(border);
            }
        }
        return result;
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

        var draggedItems = new List<OrderItem>(_draggedItems);
        var isLinkMode = _isLinkMode;
        var viewModel = FindViewModel();

        try
        {
            if (isLinkMode)
            {
                // Link mode: find target and link items together
                OrderItem? linkTarget = null;
                if (_currentLinkTargetBorder != null)
                {
                    var dc = _currentLinkTargetBorder.DataContext;
                    if (dc is OrderItem highlightedItem) linkTarget = highlightedItem;
                    else if (dc is OrderItemGroup highlightedGroup) linkTarget = highlightedGroup.First;
                }

                linkTarget ??= FindNearestLinkTarget();

                if (linkTarget != null && linkTarget.IsPracticallyEmpty)
                {
                    // Find nearest non-blank card
                    Point mousePos = Mouse.GetPosition(AssociatedObject);
                    double bestDist = double.MaxValue;
                    OrderItem? replacement = null;

                    foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
                    {
                        if (panelChild.Visibility != Visibility.Visible) continue;
                        var border = FindVisualChildOfType<Border>(panelChild);
                        if (border == null || border == _draggedElement) continue;

                        OrderItem? candidate = null;
                        if (border.DataContext is OrderItem oi) candidate = oi;
                        else if (border.DataContext is OrderItemGroup og) candidate = og.First;
                        if (candidate == null || candidate.IsPracticallyEmpty) continue;

                        var bounds = new Rect(border.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0)),
                            new Size(border.ActualWidth, border.ActualHeight));
                        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                        var dist = (center - mousePos).Length;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            replacement = candidate;
                        }
                    }
                    if (replacement != null) linkTarget = replacement;
                }

                if (LinkComplete != null)
                {
                    LinkComplete.Invoke(draggedItems, linkTarget);
                }
                else if (viewModel != null && linkTarget != null)
                {
                    await viewModel.LinkItemsAsync(draggedItems, linkTarget);
                }
            }
            else
            {
                // Reorder mode: if reordering is disabled, cancel and snap back instead
                if (!EnableReordering)
                {
                    CancelDrag();
                    // No reordering, nothing to save or notify
                }
                else
                {
                    // Item is already in correct position (moved during drag)
                    // Just save and notify
                    if (viewModel != null)
                    {
                        await viewModel.SaveAsync();
                    }

                    // Raise event for any listeners
                    ReorderComplete?.Invoke(draggedItems, null);
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Exception in drag operation!\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                "Drag Error");
        }

        // Cleanup after a short delay to allow UI to settle
        _cleanupTimer ??= new() { Interval = TimeSpan.FromMilliseconds(200) };
        _cleanupTimer.Tick -= OnCleanupTimerTick;
        _cleanupTimer.Tick += OnCleanupTimerTick;
        _cleanupTimer.Start();
    }

    private void OnCleanupTimerTick(object? sender, EventArgs e)
    {
        _cleanupTimer?.Stop();
        CleanupDragState();
        _isFinishingDrag = false;
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

        // Delay cleanup to allow animation to complete - reuse timer
        _cleanupTimer ??= new();
        _cleanupTimer.Interval = TimeSpan.FromMilliseconds(AnimationDuration + 50);
        _cleanupTimer.Tick -= OnCancelCleanupTick;
        _cleanupTimer.Tick += OnCancelCleanupTick;
        _cleanupTimer.Start();
    }

    private void OnCancelCleanupTick(object? sender, EventArgs e)
    {
        _cleanupTimer?.Stop();
        CleanupDragState();
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
            
            // Reset scale transform with animation
            if (border.RenderTransform is ScaleTransform scaleTransform)
            {
                var scaleXAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleYAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
            }
        }

        // Clear any link-mode target feedback
        ClearTargetVisualFeedback();
        _currentLinkTargetBorder = null;

        // Clear animator transforms
        _animator?.ClearTransforms();

        // Reset all state variables
        _isDragging = false;
        _draggedElement = null;
        _draggedPanelChild = null;
        _visualFeedbackBorder = null;
        _draggedItems.Clear();
        _shiftedCards.Clear();
        _draggedIndex = -1;
        _currentLogicalIndex = -1;
        _draggedTransform = null;
        _originalBorderBrush = null;
        _lockedPanelWidth = null;
        _initialClickWasOnHandle = false;
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

        var targetThickness = new Thickness(3);

        // Create a new mutable brush for animation (frozen brushes can't be animated)
        var newBrush = new SolidColorBrush(border.BorderBrush is SolidColorBrush currentBrush
            ? currentBrush.Color
            : Colors.Gray);

        border.BorderBrush = newBrush;
        border.BorderThickness = targetThickness;

        // Add drop shadow effect for all drag modes (better depth perception)
        var shadowColor = isLinkMode
            ? Color.FromRgb(138, 43, 226) // Purple glow for link
            : Color.FromRgb(0, 0, 0);      // Black shadow for reorder

        var dropShadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = shadowColor,
            BlurRadius = isLinkMode ? 25 : DRAG_SHADOW_BLUR,
            ShadowDepth = isLinkMode ? 0 : 12, // Elevated shadow for reorder, glow for link
            Direction = 270, // Shadow below
            Opacity = isLinkMode ? 0.7 : 0.35
        };
        border.Effect = dropShadow;

        // Add slight scale transform for "lifted" effect
        var scaleTransform = new ScaleTransform(1.0, 1.0);
        border.RenderTransform = scaleTransform;
        border.RenderTransformOrigin = new Point(0.5, 0.5);

        var scaleXAnimation = new DoubleAnimation
        {
            To = 1.02,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleYAnimation = new DoubleAnimation
        {
            To = 1.02,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);

        // Animate to target color
        var colorAnimation = new ColorAnimation
        {
            To = targetColor,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        newBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
    }

    // Finds the Border element under the mouse cursor (excluding the dragged element)
    private Border? FindBorderUnderCursor()
    {
        if (AssociatedObject == null || _draggedElement == null) return null;

        var mousePosition = Mouse.GetPosition(AssociatedObject);

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible)
                continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null || border == _draggedElement)
                continue;

            // If we're dragging orders (Ctrl+drag link mode), do not consider sticky-note targets
            bool draggingHasOrder = _draggedItems != null && _draggedItems.Count > 0 && _draggedItems.Any(d => d.NoteType == NoteType.Order);
            if (draggingHasOrder)
            {
                if (border.DataContext is OrderItem targetOi && targetOi.NoteType == NoteType.StickyNote)
                    continue;
                if (border.DataContext is OrderItemGroup targetOg && targetOg.First != null && targetOg.First.NoteType == NoteType.StickyNote)
                    continue;
            }

            if (!IsRenderableDataContext(border.DataContext))
                continue;

            var borderBounds = new Rect(
                border.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0)),
                new Size(border.ActualWidth, border.ActualHeight)
            );

            if (borderBounds.Contains(mousePosition))
                return border;
        }

        return null;
    }

    // Apply visual feedback to the candidate link target (non-dragged card)
    private void ApplyTargetVisualFeedback(FrameworkElement element)
    {
        if (element is not Border border) return;

        // Apply a purple dashed outline + glow so it's clear we're linking to this card
        var dashBrush = new SolidColorBrush(Color.FromRgb(138, 43, 226));
        border.BorderBrush = dashBrush;
        border.BorderThickness = new Thickness(3);

        var dropShadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(138, 43, 226),
            BlurRadius = 18,
            ShadowDepth = 0,
            Opacity = 0.75
        };
        border.Effect = dropShadow;
    }

    private void ClearTargetVisualFeedback()
    {
        if (_currentLinkTargetBorder is Border tb)
        {
            // Attempt to restore to default look - if this border is the same as the
            // dragged element's visualFeedbackBorder we will restore that separately.
            tb.BorderBrush = _originalBorderBrush ?? new SolidColorBrush(Colors.Transparent);
            tb.BorderThickness = _originalBorderThickness;
            tb.Effect = null;
        }
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
                // Prefer the outermost card Border belonging to the same panel child
                // to ensure the behavior consistently uses the full card element
                // (clicks on inner elements like the color bar should map to the
                // same top-level Border instance).
                var panelChild = FindPanelChild(border);
                if (panelChild != null)
                {
                    var outerBorder = FindVisualChildOfType<Border>(panelChild);
                    if (outerBorder != null && IsRenderableDataContext(outerBorder.DataContext))
                        return outerBorder;
                }

                return border;
            }

            // Check ContentControl and search its visual children
            if (current is ContentControl control && IsRenderableDataContext(control.DataContext))
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

    /// <summary>
    /// Checks if the click originated from an editable control (TextBox, RichTextBox, etc.)
    /// that should handle its own mouse events for text selection.
    /// </summary>
    private bool IsClickOnEditableControl(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            // Check for editable text controls
            if (current is System.Windows.Controls.Primitives.TextBoxBase || // TextBox, RichTextBox
                current is PasswordBox ||
                current is ComboBox { IsEditable: true })
            {
                return true;
            }
            
            // Check for document elements that are children of RichTextBox
            if (current is System.Windows.Documents.TextElement ||
                current is System.Windows.Documents.FlowDocument ||
                current is System.Windows.Controls.Primitives.DocumentViewerBase)
            {
                return true;
            }

            // Stop if we've reached the panel
            if (current == AssociatedObject)
                break;

            // Walk up both visual and logical trees
            var visualParent = VisualTreeHelper.GetParent(current);
            var logicalParent = LogicalTreeHelper.GetParent(current);
            
            // Prefer visual parent, but fall back to logical for document elements
            current = visualParent ?? logicalParent;
        }

        return false;
    }

    /// <summary>
    /// Checks if the click originated from a drag handle element (Tag="DragHandle").
    /// </summary>
    private bool IsClickOnDragHandle(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            // Check if this element has Tag="DragHandle"
            if (current is FrameworkElement fe && fe.Tag is string tag && tag == "DragHandle")
            {
                return true;
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
            if (border != null && IsRenderableDataContext(border.DataContext))
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
                if (bGroup.Members?.Count > 0 && eGroup.Members?.Count > 0 && bGroup.First?.Id == eGroup.First?.Id)
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

        // In swap-based reorder mode, items are swapped during drag
        // No target needed at finish time
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

            if (!IsRenderableDataContext(border.DataContext))
                continue;

            // Check if mouse is over this border
            var borderBounds = new Rect(
                border.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0)),
                new Size(border.ActualWidth, border.ActualHeight)
            );

            if (borderBounds.Contains(mousePosition))
            {
                // If dragging orders, ignore sticky-note cards
                bool draggingHasOrder = _draggedItems != null && _draggedItems.Count > 0 && _draggedItems.Any(d => d.NoteType == NoteType.Order);
                if (draggingHasOrder)
                {
                    if (border.DataContext is OrderItem oi && oi.NoteType == NoteType.StickyNote)
                        continue;
                    if (border.DataContext is OrderItemGroup og && og.First != null && og.First.NoteType == NoteType.StickyNote)
                        continue;
                }

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
                IsRenderableDataContext(border.DataContext))
            {
                children.Add(border);
            }
        }

        if (children.Count == 0) return null;

        // Compute nearest candidate by center-to-mouse distance, preferring same NoteType
        var mousePos = Mouse.GetPosition(AssociatedObject);
        double bestDist = double.MaxValue;
        FrameworkElement? best = null;

        bool draggingHasOrder = _draggedItems != null && _draggedItems.Count > 0 && _draggedItems.Any(d => d.NoteType == NoteType.Order);

        foreach (var c in children)
        {
            // If dragging orders, skip sticky-note targets
            if (draggingHasOrder)
            {
                if (c.DataContext is OrderItem oi && oi.NoteType == NoteType.StickyNote) continue;
                if (c.DataContext is OrderItemGroup og && og.First != null && og.First.NoteType == NoteType.StickyNote) continue;
            }

            var bounds = new Rect(c.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0)), new Size(c.ActualWidth, c.ActualHeight));
            var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            var dist = (center - mousePos).Length;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = c;
            }
        }

        if (best == null)
        {
            // fallback to last element if nothing closer found
            best = children.LastOrDefault();
            if (best == null) return null;
        }

        if (best.DataContext is OrderItem bi)
            return bi;
        if (best.DataContext is OrderItemGroup bg)
            return bg.First;

        return null;
    }

    private static bool IsRenderableDataContext(object? dc)
    {
        if (dc == null) return false;
        if (dc is OrderItem oi)
            return oi.IsRenderable;
        if (dc is OrderItemGroup og)
            return og.Members != null && og.Members.Count > 0 && og.First?.IsRenderable == true;
        return false;
    }
}
