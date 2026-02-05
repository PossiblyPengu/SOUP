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
/// iOS-style sliding reorder behavior for OrderLog cards.
/// <para>
/// Features:
/// - Center-based swap detection (more intuitive)
/// - Temporal swap cooldown (prevents oscillation)
/// - Staggered cascade animations (polished feel)
/// - Spring physics with overshoot (iOS-authentic)
/// - Ctrl+Drag to link items instead of reorder
/// </para>
/// </summary>
public class SlidingReorderBehavior : Behavior<Panel>
{
    #region Constants

    private const double DRAG_SCALE = 1.03;
    private const int DRAG_Z_INDEX = 100;
    private const double DRAG_SHADOW_BLUR = 25;
    private const int SWAP_COOLDOWN_MS = 120;
    private const int STAGGER_DELAY_MS = 12;
    private const double SPRING_OVERSHOOT = 1.08; // 8% overshoot for spring feel

    #endregion

    #region State

    private Point _dragStartPoint;
    private Point _elementClickOffset;
    private Point _elementOriginalPosition;
    private bool _isDragging;
    private bool _isFinishingDrag;
    private FrameworkElement? _draggedElement;
    private FrameworkElement? _draggedPanelChild;
    private FrameworkElement? _visualFeedbackBorder;
    private List<OrderItem> _draggedItems = [];
    private int _draggedIndex = -1;
    private int _currentLogicalIndex = -1;
    private TransformGroup? _draggedTransform;
    private bool _isLinkMode;
    private bool _initialClickWasOnHandle;
    private FrameworkElement? _currentLinkTargetBorder;
    private DateTime _lastSwapTime = DateTime.MinValue;
    private Brush? _originalBorderBrush;
    private Thickness _originalBorderThickness;
    private System.Windows.Threading.DispatcherTimer? _cleanupTimer;

    // Animation transforms for neighbor cards
    private readonly Dictionary<FrameworkElement, TranslateTransform> _neighborTransforms = [];

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty AnimationDurationProperty =
        DependencyProperty.Register(
            nameof(AnimationDuration),
            typeof(double),
            typeof(SlidingReorderBehavior),
            new PropertyMetadata(180.0));

    public static readonly DependencyProperty RequireDragHandleProperty =
        DependencyProperty.Register(
            nameof(RequireDragHandle),
            typeof(bool),
            typeof(SlidingReorderBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty EnableReorderingProperty =
        DependencyProperty.Register(
            nameof(EnableReordering),
            typeof(bool),
            typeof(SlidingReorderBehavior),
            new PropertyMetadata(true));

    public static readonly DependencyProperty LockHorizontalPositionProperty =
        DependencyProperty.Register(
            nameof(LockHorizontalPosition),
            typeof(bool),
            typeof(SlidingReorderBehavior),
            new PropertyMetadata(true));

    /// <summary>Animation duration in milliseconds (default: 180ms)</summary>
    public double AnimationDuration
    {
        get => (double)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>When true, drag can only be initiated from elements with Tag="DragHandle".</summary>
    public bool RequireDragHandle
    {
        get => (bool)GetValue(RequireDragHandleProperty);
        set => SetValue(RequireDragHandleProperty, value);
    }

    /// <summary>When false, reordering is disabled and only link-mode (Ctrl+Drag) is allowed.</summary>
    public bool EnableReordering
    {
        get => (bool)GetValue(EnableReorderingProperty);
        set => SetValue(EnableReorderingProperty, value);
    }

    /// <summary>When true (default), cards only move vertically during drag.</summary>
    public bool LockHorizontalPosition
    {
        get => (bool)GetValue(LockHorizontalPositionProperty);
        set => SetValue(LockHorizontalPositionProperty, value);
    }

    #endregion

    #region Events

    /// <summary>Event raised when a reorder operation completes.</summary>
    public event Action<List<OrderItem>, OrderItem?>? ReorderComplete;

    /// <summary>Event raised when a link operation completes.</summary>
    public event Action<List<OrderItem>, OrderItem?>? LinkComplete;

    #endregion

    #region Lifecycle

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject == null) return;

        AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        }

        CancelDrag();
        base.OnDetaching();
    }

    #endregion

    #region Mouse Event Handlers

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging || _isFinishingDrag) return;

        // Don't interfere with editable controls
        if (IsClickOnEditableControl(e.OriginalSource as DependencyObject)) return;

        // Check section drag handle for legacy Ctrl+drag behavior
        if (IsClickOnSectionDragHandle(e.OriginalSource as DependencyObject))
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) return;
        }

        _initialClickWasOnHandle = IsClickOnDragHandle(e.OriginalSource as DependencyObject);
        _dragStartPoint = e.GetPosition(AssociatedObject);

        _draggedElement = FindCardElement(e.OriginalSource as DependencyObject);
        if (_draggedElement != null)
        {
            _draggedPanelChild = FindPanelChildForElement(_draggedElement);
            var clickTarget = _draggedPanelChild ?? _draggedElement;
            _elementClickOffset = e.GetPosition(clickTarget);
            _draggedItems = ExtractOrderItems(_draggedElement);
            _draggedIndex = GetElementIndex(_draggedElement);
            _currentLogicalIndex = _draggedIndex;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (_isDragging) FinishDrag();
            return;
        }

        var currentPosition = e.GetPosition(AssociatedObject);

        if (!_isDragging)
        {
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
        if (_isDragging) FinishDrag();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isDragging) CancelDrag();
    }

    #endregion

    #region Drag Operations

    private void StartDrag()
    {
        if (_draggedElement == null || AssociatedObject == null) return;

        // Require handle click or Ctrl for linking
        if (!_initialClickWasOnHandle && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        _isDragging = true;
        _currentLogicalIndex = _draggedIndex;
        _isLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        _lastSwapTime = DateTime.MinValue;

        _draggedPanelChild ??= FindPanelChild(_draggedElement);
        var transformTarget = _draggedPanelChild ?? _draggedElement;
        _elementOriginalPosition = transformTarget.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));

        Mouse.Capture(AssociatedObject, CaptureMode.SubTree);
        ApplyDragTransform(transformTarget);

        if (_draggedPanelChild != null)
            Panel.SetZIndex(_draggedPanelChild, DRAG_Z_INDEX);

        _visualFeedbackBorder = _draggedPanelChild != null
            ? FindVisualChildOfType<Border>(_draggedPanelChild)
            : _draggedElement;

        if (_visualFeedbackBorder != null)
            ApplyDragVisualFeedback(_visualFeedbackBorder, _isLinkMode);
    }

    private void UpdateDrag(Point currentPosition)
    {
        if (_draggedElement == null || AssociatedObject == null || _isFinishingDrag) return;

        // Check for mode change
        bool currentLinkMode = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (currentLinkMode != _isLinkMode)
        {
            _isLinkMode = currentLinkMode;
            if (_visualFeedbackBorder != null)
                ApplyDragVisualFeedback(_visualFeedbackBorder, _isLinkMode);
        }

        // Link mode: move card and highlight target
        if (_isLinkMode)
        {
            UpdateDraggedElementPosition(currentPosition);
            UpdateLinkTargetHighlight();
            return;
        }

        // Reorder mode: move card and perform sliding swaps
        UpdateDraggedElementPosition(currentPosition);

        if (EnableReordering)
            PerformSlidingReorder();
    }

    private void UpdateLinkTargetHighlight()
    {
        var targetBorder = FindBorderUnderCursor();
        if (!ReferenceEquals(targetBorder, _currentLinkTargetBorder))
        {
            ClearTargetVisualFeedback();
            if (targetBorder != null)
                ApplyTargetVisualFeedback(targetBorder);
            _currentLinkTargetBorder = targetBorder;
        }
    }

    /// <summary>
    /// Core sliding reorder algorithm with center-based swap detection,
    /// temporal cooldown, and staggered animations.
    /// </summary>
    private void PerformSlidingReorder()
    {
        if (_draggedElement == null || AssociatedObject == null) return;

        var viewModel = FindViewModel();
        if (viewModel == null) return;

        var draggedItem = _draggedItems.FirstOrDefault();
        if (draggedItem == null) return;

        // Check swap cooldown
        if ((DateTime.Now - _lastSwapTime).TotalMilliseconds < SWAP_COOLDOWN_MS)
            return;

        // Calculate dragged card's center Y
        var transformTarget = _draggedPanelChild ?? _draggedElement;
        var translateTransform = GetTranslateTransform();
        double draggedCenterY = _elementOriginalPosition.Y
            + (translateTransform?.Y ?? 0)
            + (transformTarget.ActualHeight / 2);

        var children = GetAllCardBorders();
        if (children.Count == 0) return;

        // Find swap target using center-based detection
        int? swapTargetIndex = null;

        for (int i = 0; i < children.Count; i++)
        {
            var card = children[i];
            var cardItem = GetOrderItemFromElement(card);
            if (cardItem != null && cardItem.Id == draggedItem.Id) continue;

            var pos = card.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));
            double cardCenterY = pos.Y + (card.ActualHeight / 2);

            // Center-based detection: swap when dragged center passes neighbor center
            if (i < _currentLogicalIndex && draggedCenterY < cardCenterY)
            {
                // Moving up - swap with first card we pass
                swapTargetIndex = i;
                break;
            }
            else if (i > _currentLogicalIndex && draggedCenterY > cardCenterY)
            {
                // Moving down - keep track of furthest card we pass
                swapTargetIndex = i;
            }
        }

        if (swapTargetIndex.HasValue && swapTargetIndex.Value != _currentLogicalIndex)
        {
            int targetIndex = swapTargetIndex.Value;

            // Animate neighbors with staggered delays
            AnimateNeighborsForSwap(children, _currentLogicalIndex, targetIndex, draggedItem);

            // Store transform state
            var currentTransform = GetTranslateTransform();
            double currentOffsetY = currentTransform?.Y ?? 0;
            double currentOffsetX = currentTransform?.X ?? 0;

            // Move item in collection
            viewModel.MoveItemToIndex(draggedItem, targetIndex);
            _currentLogicalIndex = targetIndex;
            _draggedIndex = targetIndex;
            _lastSwapTime = DateTime.Now;

            // Force layout and re-acquire element
            AssociatedObject.UpdateLayout();
            ReacquireDraggedElement(draggedItem, currentOffsetX, currentOffsetY);

            // Clear neighbor transforms after swap (they're now in correct positions)
            ClearNeighborTransformsAnimated();
        }
    }

    /// <summary>
    /// Animates neighbor cards with staggered delays for a cascading effect.
    /// </summary>
    private void AnimateNeighborsForSwap(List<FrameworkElement> children, int fromIndex, int toIndex, OrderItem draggedItem)
    {
        bool movingDown = toIndex > fromIndex;
        int startIdx = movingDown ? fromIndex + 1 : toIndex;
        int endIdx = movingDown ? toIndex : fromIndex - 1;

        var affectedCards = new List<(FrameworkElement card, int delay)>();
        int delayCounter = 0;

        for (int i = 0; i < children.Count; i++)
        {
            var card = children[i];
            var cardItem = GetOrderItemFromElement(card);
            if (cardItem != null && cardItem.Id == draggedItem.Id) continue;

            // Check if card is in the affected range
            bool isAffected = i >= startIdx && i <= endIdx;

            if (isAffected)
            {
                affectedCards.Add((card, delayCounter * STAGGER_DELAY_MS));
                delayCounter++;
            }
        }

        // Calculate slide distance based on dragged card's height
        var dragTarget = _draggedPanelChild ?? _draggedElement!;
        double slideDistance = dragTarget.ActualHeight + dragTarget.Margin.Top + dragTarget.Margin.Bottom;
        double direction = movingDown ? -1 : 1; // Neighbors move opposite to drag direction

        foreach (var (card, delay) in affectedCards)
        {
            AnimateCardSlide(card, slideDistance * direction, TimeSpan.FromMilliseconds(delay));
        }
    }

    /// <summary>
    /// Animates a single card with spring physics (overshoot effect).
    /// </summary>
    private void AnimateCardSlide(FrameworkElement element, double targetOffset, TimeSpan delay)
    {
        if (!_neighborTransforms.TryGetValue(element, out var transform))
        {
            transform = new TranslateTransform();
            _neighborTransforms[element] = transform;

            var existing = element.RenderTransform;
            if (existing == null || existing == Transform.Identity)
            {
                element.RenderTransform = transform;
            }
            else if (existing is TransformGroup group)
            {
                group.Children.Add(transform);
            }
            else
            {
                var newGroup = new TransformGroup();
                newGroup.Children.Add(existing);
                newGroup.Children.Add(transform);
                element.RenderTransform = newGroup;
            }
        }

        // Spring animation with overshoot
        var duration = TimeSpan.FromMilliseconds(AnimationDuration);
        
        // First animate to overshoot position
        var overshootValue = targetOffset * SPRING_OVERSHOOT;
        var overshootDuration = TimeSpan.FromMilliseconds(AnimationDuration * 0.6);
        
        var overshootAnim = new DoubleAnimation(overshootValue, overshootDuration)
        {
            BeginTime = delay,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        // Then settle to final position
        var settleAnim = new DoubleAnimation(targetOffset, TimeSpan.FromMilliseconds(AnimationDuration * 0.4))
        {
            BeginTime = delay + overshootDuration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        settleAnim.Completed += (s, e) =>
        {
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = targetOffset;
        };

        // Use storyboard for sequenced animations
        var storyboard = new Storyboard();
        Storyboard.SetTarget(overshootAnim, element);
        Storyboard.SetTargetProperty(overshootAnim, new PropertyPath("RenderTransform.Y"));
        
        // For simplicity, just use the overshoot animation with elastic easing
        var elasticAnim = new DoubleAnimation(targetOffset, duration)
        {
            BeginTime = delay,
            EasingFunction = new ElasticEase 
            { 
                EasingMode = EasingMode.EaseOut,
                Oscillations = 1,
                Springiness = 8
            }
        };

        elasticAnim.Completed += (s, e) =>
        {
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = targetOffset;
        };

        transform.BeginAnimation(TranslateTransform.YProperty, elasticAnim);
    }

    private void ClearNeighborTransformsAnimated()
    {
        var duration = TimeSpan.FromMilliseconds(AnimationDuration * 0.5);

        foreach (var (element, transform) in _neighborTransforms.ToList())
        {
            var animation = new DoubleAnimation(0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) =>
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = 0;
            };

            transform.BeginAnimation(TranslateTransform.YProperty, animation);
        }
    }

    private async void FinishDrag()
    {
        if (!_isDragging || _draggedElement == null || AssociatedObject == null || _isFinishingDrag)
        {
            CleanupDragState();
            return;
        }

        _isFinishingDrag = true;
        _isDragging = false;
        Mouse.Capture(null);

        var draggedItems = new List<OrderItem>(_draggedItems);
        var isLinkMode = _isLinkMode;
        var viewModel = FindViewModel();

        try
        {
            if (isLinkMode)
            {
                // Link mode
                OrderItem? linkTarget = null;
                if (_currentLinkTargetBorder != null)
                {
                    var dc = _currentLinkTargetBorder.DataContext;
                    if (dc is OrderItem item) linkTarget = item;
                    else if (dc is OrderItemGroup group) linkTarget = group.First;
                }

                linkTarget ??= FindNearestLinkTarget();

                if (LinkComplete != null)
                    LinkComplete.Invoke(draggedItems, linkTarget);
                else if (viewModel != null && linkTarget != null)
                    await viewModel.LinkItemsAsync(draggedItems, linkTarget);
            }
            else
            {
                // Reorder mode
                if (!EnableReordering)
                {
                    CancelDrag();
                }
                else
                {
                    if (viewModel != null)
                        await viewModel.SaveAsync();
                    ReorderComplete?.Invoke(draggedItems, null);
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Drag error: {ex.Message}", "Error");
        }

        // Delayed cleanup
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

    private void CancelDrag()
    {
        if (!_isDragging) return;
        Mouse.Capture(null);

        // Animate back
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

        ClearNeighborTransformsAnimated();

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

    #endregion

    #region Transform Helpers

    private void ApplyDragTransform(FrameworkElement element)
    {
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

    private TranslateTransform? GetTranslateTransform()
    {
        return _draggedTransform?.Children.OfType<TranslateTransform>().FirstOrDefault();
    }

    private void UpdateDraggedElementPosition(Point currentPosition)
    {
        if (_draggedElement == null || AssociatedObject == null || _isFinishingDrag) return;

        var translateTransform = GetTranslateTransform();
        if (translateTransform == null) return;

        var desiredX = currentPosition.X - _elementClickOffset.X;
        var desiredY = currentPosition.Y - _elementClickOffset.Y;

        var panelBounds = new Rect(0, 0, AssociatedObject.ActualWidth, AssociatedObject.ActualHeight);
        var sizeTarget = _draggedPanelChild ?? _draggedElement!;

        const double margin = 10;
        desiredX = Math.Max(margin, Math.Min(desiredX, panelBounds.Width - sizeTarget.ActualWidth - margin));
        desiredY = Math.Max(margin, desiredY);

        translateTransform.X = LockHorizontalPosition ? 0 : (desiredX - _elementOriginalPosition.X);
        translateTransform.Y = desiredY - _elementOriginalPosition.Y;
    }

    private void ReacquireDraggedElement(OrderItem draggedItem, double offsetX, double offsetY)
    {
        if (AssociatedObject == null) return;

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null) continue;

            var item = GetOrderItemFromElement(border);
            if (item != null && item.Id == draggedItem.Id)
            {
                _draggedElement = border;
                _draggedPanelChild = panelChild;
                _elementOriginalPosition = panelChild.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0));

                ApplyDragTransform(panelChild);
                var newTransform = GetTranslateTransform();
                if (newTransform != null)
                {
                    newTransform.X = offsetX;
                    newTransform.Y = offsetY;
                }

                Panel.SetZIndex(panelChild, DRAG_Z_INDEX);
                _visualFeedbackBorder = border;
                ApplyDragVisualFeedback(border, _isLinkMode);
                return;
            }
        }
    }

    #endregion

    #region Visual Feedback

    private void ApplyDragVisualFeedback(FrameworkElement element, bool isLinkMode)
    {
        if (element is not Border border) return;

        if (_originalBorderBrush == null)
        {
            _originalBorderBrush = border.BorderBrush;
            _originalBorderThickness = border.BorderThickness;
        }

        var targetColor = isLinkMode
            ? Color.FromRgb(138, 43, 226) // Purple for link
            : Color.FromRgb(34, 197, 94);  // Green for reorder

        var newBrush = new SolidColorBrush(border.BorderBrush is SolidColorBrush cb ? cb.Color : Colors.Gray);
        border.BorderBrush = newBrush;
        border.BorderThickness = new Thickness(3);

        var shadowColor = isLinkMode ? Color.FromRgb(138, 43, 226) : Color.FromRgb(0, 0, 0);
        border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = shadowColor,
            BlurRadius = isLinkMode ? 25 : DRAG_SHADOW_BLUR,
            ShadowDepth = isLinkMode ? 0 : 10,
            Direction = 270,
            Opacity = isLinkMode ? 0.7 : 0.3
        };

        var scaleTransform = new ScaleTransform(1.0, 1.0);
        border.RenderTransform = scaleTransform;
        border.RenderTransformOrigin = new Point(0.5, 0.5);

        var scaleAnim = new DoubleAnimation(1.02, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        var colorAnim = new ColorAnimation(targetColor, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        newBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
    }

    private void ApplyTargetVisualFeedback(FrameworkElement element)
    {
        if (element is not Border border) return;

        border.BorderBrush = new SolidColorBrush(Color.FromRgb(138, 43, 226));
        border.BorderThickness = new Thickness(3);
        border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(138, 43, 226),
            BlurRadius = 18,
            ShadowDepth = 0,
            Opacity = 0.75
        };
    }

    private void ClearTargetVisualFeedback()
    {
        if (_currentLinkTargetBorder is Border tb)
        {
            tb.BorderBrush = _originalBorderBrush ?? new SolidColorBrush(Colors.Transparent);
            tb.BorderThickness = _originalBorderThickness;
            tb.Effect = null;
        }
    }

    private void CleanupDragState()
    {
        var transformTarget = _draggedPanelChild ?? _draggedElement;
        if (transformTarget != null)
            transformTarget.RenderTransform = null;

        if (_draggedPanelChild != null)
            Panel.SetZIndex(_draggedPanelChild, 0);

        if (_visualFeedbackBorder is Border border)
        {
            border.BorderBrush = _originalBorderBrush;
            border.BorderThickness = _originalBorderThickness;
            border.Effect = null;

            if (border.RenderTransform is ScaleTransform st)
            {
                var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }
        }

        ClearTargetVisualFeedback();
        _currentLinkTargetBorder = null;

        // Clear neighbor transforms
        foreach (var (element, _) in _neighborTransforms)
            element.RenderTransform = null;
        _neighborTransforms.Clear();

        _isDragging = false;
        _draggedElement = null;
        _draggedPanelChild = null;
        _visualFeedbackBorder = null;
        _draggedItems.Clear();
        _draggedIndex = -1;
        _currentLogicalIndex = -1;
        _draggedTransform = null;
        _originalBorderBrush = null;
        _initialClickWasOnHandle = false;
    }

    #endregion

    #region Helper Methods

    private OrderItem? GetOrderItemFromElement(FrameworkElement element)
    {
        if (element.DataContext is OrderItem item) return item;
        if (element.DataContext is OrderItemGroup group) return group.First;
        return null;
    }

    private List<FrameworkElement> GetAllCardBorders()
    {
        var result = new List<FrameworkElement>();
        if (AssociatedObject == null) return result;

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;
            var border = FindVisualChildOfType<Border>(panelChild);
            if (border != null && IsRenderableDataContext(border.DataContext))
                result.Add(border);
        }
        return result;
    }

    private static bool IsRenderableDataContext(object? dataContext)
    {
        return dataContext is OrderItem || dataContext is OrderItemGroup;
    }

    private int GetElementIndex(FrameworkElement element)
    {
        if (AssociatedObject == null) return -1;

        int index = 0;
        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;
            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == element) return index;
            if (border != null && IsRenderableDataContext(border.DataContext))
                index++;
        }
        return -1;
    }

    private FrameworkElement? FindPanelChild(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current != null && current != AssociatedObject)
        {
            if (current is FrameworkElement fe && AssociatedObject?.Children.Contains(fe) == true)
                return fe;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private FrameworkElement? FindPanelChildForElement(FrameworkElement element)
    {
        if (AssociatedObject == null || element == null) return null;

        var elementData = element.DataContext;

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;
            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null) continue;

            var bd = border.DataContext;
            if (ReferenceEquals(bd, elementData)) return panelChild;

            if (bd is OrderItem bItem && elementData is OrderItem eItem && bItem.Id == eItem.Id)
                return panelChild;

            if (bd is OrderItemGroup bGroup && elementData is OrderItemGroup eGroup)
            {
                if (bGroup.LinkedGroupId != null && bGroup.LinkedGroupId == eGroup.LinkedGroupId)
                    return panelChild;
                if (bGroup.Members?.Count > 0 && eGroup.Members?.Count > 0 && bGroup.First?.Id == eGroup.First?.Id)
                    return panelChild;
            }
        }

        return FindPanelChild(element);
    }

    private List<OrderItem> ExtractOrderItems(FrameworkElement element)
    {
        var result = new List<OrderItem>();
        if (element.DataContext is OrderItem item)
            result.Add(item);
        else if (element.DataContext is OrderItemGroup group)
            result.AddRange(group.Members);
        return result;
    }

    private FrameworkElement? FindCardElement(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is Border border && (border.DataContext is OrderItem || border.DataContext is OrderItemGroup))
            {
                var panelChild = FindPanelChild(border);
                if (panelChild != null)
                {
                    var outerBorder = FindVisualChildOfType<Border>(panelChild);
                    if (outerBorder != null && IsRenderableDataContext(outerBorder.DataContext))
                        return outerBorder;
                }
                return border;
            }

            if (current is ContentControl control && IsRenderableDataContext(control.DataContext))
            {
                var childBorder = FindVisualChildOfType<Border>(control);
                if (childBorder != null) return childBorder;
            }

            if (current == AssociatedObject) break;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindVisualChildOfType<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var result = FindVisualChildOfType<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private Border? FindBorderUnderCursor()
    {
        if (AssociatedObject == null || _draggedElement == null) return null;

        var mousePosition = Mouse.GetPosition(AssociatedObject);

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;

            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null || border == _draggedElement) continue;

            bool draggingHasOrder = _draggedItems.Any(d => d.NoteType == NoteType.Order);
            if (draggingHasOrder)
            {
                if (border.DataContext is OrderItem oi && oi.NoteType == NoteType.StickyNote) continue;
                if (border.DataContext is OrderItemGroup og && og.First?.NoteType == NoteType.StickyNote) continue;
            }

            if (!IsRenderableDataContext(border.DataContext)) continue;

            var borderBounds = new Rect(
                border.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0)),
                new Size(border.ActualWidth, border.ActualHeight));

            if (borderBounds.Contains(mousePosition))
                return border;
        }
        return null;
    }

    private OrderItem? FindNearestLinkTarget()
    {
        if (AssociatedObject == null) return null;

        var mousePos = Mouse.GetPosition(AssociatedObject);
        double bestDist = double.MaxValue;
        OrderItem? best = null;

        foreach (var panelChild in AssociatedObject.Children.OfType<FrameworkElement>())
        {
            if (panelChild.Visibility != Visibility.Visible) continue;
            var border = FindVisualChildOfType<Border>(panelChild);
            if (border == null || border == _draggedElement) continue;

            OrderItem? candidate = null;
            if (border.DataContext is OrderItem oi) candidate = oi;
            else if (border.DataContext is OrderItemGroup og) candidate = og.First;
            if (candidate == null || candidate.IsPracticallyEmpty) continue;

            var bounds = new Rect(
                border.TransformToAncestor(AssociatedObject).Transform(new Point(0, 0)),
                new Size(border.ActualWidth, border.ActualHeight));
            var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            var dist = (center - mousePos).Length;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }
        return best;
    }

    private OrderLogViewModel? FindViewModel()
    {
        DependencyObject? current = AssociatedObject;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is OrderLogViewModel vm)
                return vm;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private bool IsClickOnEditableControl(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.TextBoxBase ||
                current is PasswordBox ||
                current is ComboBox { IsEditable: true } ||
                current is System.Windows.Documents.TextElement ||
                current is System.Windows.Documents.FlowDocument)
            {
                return true;
            }
            if (current == AssociatedObject) break;
            var vp = VisualTreeHelper.GetParent(current);
            current = vp ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private bool IsClickOnSectionDragHandle(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is Border border)
            {
                var tooltip = ToolTipService.GetToolTip(border);
                if (tooltip is string s && s.Contains("Drag to move this order separately", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            if (current == AssociatedObject) break;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private bool IsClickOnDragHandle(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.Tag is string tag && tag == "DragHandle")
                return true;
            if (current == AssociatedObject) break;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    #endregion
}
