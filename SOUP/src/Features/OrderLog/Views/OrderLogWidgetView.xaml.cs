using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.Constants;
using SOUP.Services;

namespace SOUP.Features.OrderLog.Views;

/// <summary>
/// Full-featured widget view for Order Log - designed for AppBar docking
/// </summary>
public partial class OrderLogWidgetView : UserControl
{
    public event EventHandler? OpenFullViewRequested;
    private bool _nowPlayingExpanded = false;
    private bool _showingArchivedTab = false;
    private SpotifyService? _spotifyService;
    private System.Windows.Threading.DispatcherTimer? _equalizerTimer;
    private Random _random = new Random();

    public OrderLogWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        InitializeEqualizerTimer();
    }

    private void ActiveTab_Click(object sender, RoutedEventArgs e)
    {
        _showingArchivedTab = false;
        UpdateTabState();
    }

    private void ArchivedTab_Click(object sender, RoutedEventArgs e)
    {
        _showingArchivedTab = true;
        UpdateTabState();
    }

    private void UpdateTabState()
    {
        // Update tab button styles - using pill-style tabs
        if (_showingArchivedTab)
        {
            ActiveTabButton.Background = System.Windows.Media.Brushes.Transparent;
            ActiveTabButton.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
            ActiveTabButton.FontWeight = FontWeights.Normal;
            ArchivedTabButton.Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush");
            ArchivedTabButton.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");
            ArchivedTabButton.FontWeight = FontWeights.SemiBold;
            
            ActiveItemsPanel.Visibility = Visibility.Collapsed;
            ArchivedItemsPanel.Visibility = Visibility.Visible;
            AddButtonsPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ActiveTabButton.Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush");
            ActiveTabButton.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");
            ActiveTabButton.FontWeight = FontWeights.SemiBold;
            ArchivedTabButton.Background = System.Windows.Media.Brushes.Transparent;
            ArchivedTabButton.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
            ArchivedTabButton.FontWeight = FontWeights.Normal;
            
            ActiveItemsPanel.Visibility = Visibility.Visible;
            ArchivedItemsPanel.Visibility = Visibility.Collapsed;
            AddButtonsPanel.Visibility = Visibility.Visible;
        }
    }

    private void InitializeEqualizerTimer()
    {
        _equalizerTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _equalizerTimer.Tick += (s, e) => AnimateEqualizerBars();
    }

    private void NowPlayingContent_MouseEnter(object sender, MouseEventArgs e)
    {
        // Fade in the control bar
        if (ControlBar == null) return;
        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ControlBar.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void NowPlayingContent_MouseLeave(object sender, MouseEventArgs e)
    {
        // Fade out the control bar
        if (ControlBar == null) return;
        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        ControlBar.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void AnimateEqualizerBars()
    {
        if (EqBar1 == null) return;
        
        // Animate each bar to a random height
        AnimateBar(EqBar1, 0.3 + _random.NextDouble() * 0.7);
        AnimateBar(EqBar2, 0.3 + _random.NextDouble() * 0.7);
        AnimateBar(EqBar3, 0.3 + _random.NextDouble() * 0.7);
    }

    private void AnimateBar(System.Windows.Shapes.Rectangle bar, double targetScale)
    {
        if (bar.RenderTransform is ScaleTransform scale)
        {
            var animation = new DoubleAnimation(targetScale, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize Spotify service
        await InitializeSpotifyAsync();

        // Wire up fluid drag behavior events
        WireUpFluidDragBehavior();
    }

    private void WireUpFluidDragBehavior()
    {
        // Find the ItemsControl and its panel
        var itemsControl = FindVisualChild<ItemsControl>(ActiveItemsPanel);
        if (itemsControl == null)
        {
            return;
        }

        // Wait for the panel to be generated
        itemsControl.Loaded += (s, e) =>
        {
            var panel = FindVisualChild<Panel>(itemsControl);
            if (panel == null)
            {
                return;
            }

            // Find attached behaviors (support legacy fluid drag and new GridDragBehavior)
            var behaviors = Microsoft.Xaml.Behaviors.Interaction.GetBehaviors(panel);
            var fluidDragBehavior = behaviors.OfType<Behaviors.OrderLogFluidDragBehavior>().FirstOrDefault();
            var gridDrag = behaviors.OfType<Behaviors.GridDragBehavior>().FirstOrDefault();

            if (fluidDragBehavior != null)
            {
                fluidDragBehavior.ReorderComplete += OnFluidDragReorderComplete;
                fluidDragBehavior.LinkComplete += OnFluidDragLinkComplete;
            }

            if (gridDrag != null)
            {
                gridDrag.ReorderComplete += OnFluidDragReorderComplete;
            }
        };
    }

    private async void OnFluidDragReorderComplete(List<OrderItem> items, OrderItem? target)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            await vm.MoveOrdersAsync(items, target);
            vm.StatusMessage = $"Reordered {items.Count} item(s)";
        }
    }

    private async void OnFluidDragLinkComplete(List<OrderItem> items, OrderItem? target)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            await vm.LinkItemsAsync(items, target);
            vm.StatusMessage = $"Linked {items.Count} item(s)";
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Clean up
        _equalizerTimer?.Stop();
        if (_spotifyService != null)
        {
            _spotifyService.PropertyChanged -= SpotifyService_PropertyChanged;
        }
    }

    private async Task InitializeSpotifyAsync()
    {
        try
        {
            _spotifyService = SpotifyService.Instance;
            await _spotifyService.InitializeAsync();
            _spotifyService.PropertyChanged += SpotifyService_PropertyChanged;
            UpdateNowPlayingUI();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize Spotify service in widget");
        }
    }

    private void SpotifyService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateNowPlayingUI());
    }

    private void UpdateNowPlayingUI()
    {
        if (_spotifyService == null) return;

        // Hide the entire player section when nothing is playing
        NowPlayingSection.Visibility = _spotifyService.HasMedia ? Visibility.Visible : Visibility.Collapsed;
        
        if (!_spotifyService.HasMedia) return;

        TrackTitleText.Text = _spotifyService.TrackTitle;
        ArtistNameText.Text = _spotifyService.ArtistName;
        AlbumArtImage.Source = _spotifyService.AlbumArt;
        AlbumArtBlurredBg.Source = _spotifyService.AlbumArt; // Set blurred background too
        PlayPauseButton.Content = _spotifyService.IsPlaying ? "⏸" : "▶";

        // Show/hide album art placeholder
        AlbumArtPlaceholder.Visibility = _spotifyService.AlbumArt == null 
            ? Visibility.Visible 
            : Visibility.Collapsed;

        // Control equalizer animation
        if (_spotifyService.IsPlaying)
        {
            EqualizerPanel.Visibility = Visibility.Visible;
            MusicIcon.Visibility = Visibility.Collapsed;
            _equalizerTimer?.Start();
        }
        else
        {
            _equalizerTimer?.Stop();
            EqualizerPanel.Visibility = Visibility.Collapsed;
            MusicIcon.Visibility = Visibility.Visible;
        }

        // Update header text with current track if playing
        if (!string.IsNullOrEmpty(_spotifyService.TrackTitle))
        {
            if (_nowPlayingExpanded)
            {
                NowPlayingHeaderText.Text = "Now Playing";
            }
            else
            {
                // Show "Artist - Track" in collapsed mode
                var artist = _spotifyService.ArtistName;
                var track = _spotifyService.TrackTitle;
                var display = !string.IsNullOrEmpty(artist) ? $"{artist} - {track}" : track;
                
                // Truncate if too long
                NowPlayingHeaderText.Text = display.Length > 30 
                    ? display.Substring(0, 27) + "..." 
                    : display;
            }
        }
        else
        {
            NowPlayingHeaderText.Text = "Now Playing";
        }
    }

    private void NowPlayingHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click opens Spotify
            OpenSpotify();
            return;
        }

        _nowPlayingExpanded = !_nowPlayingExpanded;
        NowPlayingToggleIcon.Text = _nowPlayingExpanded ? "▼" : "▲";
        
        // Calculate target height based on widget width (for square-ish album art)
        double targetHeight = Math.Min(Math.Max(this.ActualWidth * 0.8, 180), 280);
        
        // Animated expand/collapse
        if (_nowPlayingExpanded)
        {
            NowPlayingContent.Visibility = Visibility.Visible;
            NowPlayingContent.BeginAnimation(HeightProperty, null); // Clear previous animation
            var expandAnimation = new DoubleAnimation(0, targetHeight, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            NowPlayingContent.BeginAnimation(HeightProperty, expandAnimation);
        }
        else
        {
            var currentHeight = NowPlayingContent.ActualHeight;
            var collapseAnimation = new DoubleAnimation(currentHeight, 0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            collapseAnimation.Completed += (s, _) => 
            {
                NowPlayingContent.Visibility = Visibility.Collapsed;
                NowPlayingContent.BeginAnimation(HeightProperty, null); // Clear animation to allow auto-sizing
            };
            NowPlayingContent.BeginAnimation(HeightProperty, collapseAnimation);
        }
        UpdateNowPlayingUI();
    }

    private System.Windows.Point _dragStartPoint;

    private void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            _dragStartPoint = e.GetPosition(null);
        }
    }

    private void Item_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is FrameworkElement fe && fe.DataContext is OrderItem order)
        {
            if (DataContext is OrderLogViewModel vm && vm.SelectedItems.Count > 1 && vm.SelectedItems.Contains(order))
            {
                var ids = vm.SelectedItems.Select(i => i.Id).ToArray();
                var data = new DataObject();
                data.SetData("OrderItemIds", ids);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
            }
            else
            {
                var data = new DataObject("OrderItemId", order.Id);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
            }
        }
    }

    private void Item_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("OrderItemId"))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        if (sender is Border b)
        {
            if (b.Tag == null) b.Tag = b.BorderBrush;
            b.BorderBrush = (System.Windows.Media.Brush)Application.Current?.Resources["SuccessBrush"] ?? System.Windows.Media.Brushes.LightGreen;
        }
        e.Handled = true;
    }

    private void Item_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b && b.Tag is System.Windows.Media.Brush orig)
        {
            b.BorderBrush = orig;
            b.Tag = null;
        }
    }

    private async void Item_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent("OrderItemId") && !e.Data.GetDataPresent("OrderItemIds")) return;

            var droppedIds = new System.Collections.Generic.List<Guid>();
            if (e.Data.GetDataPresent("OrderItemIds") && e.Data.GetData("OrderItemIds") is Guid[] arr)
            {
                droppedIds.AddRange(arr);
            }
            else if (e.Data.GetDataPresent("OrderItemId"))
            {
                droppedIds.Add((Guid)e.Data.GetData("OrderItemId"));
            }

            OrderItem? dropped = null;

            if (DataContext is OrderLogViewModel vm)
            {
                var droppedItems = vm.Items.Concat(vm.ArchivedItems).Where(i => droppedIds.Contains(i.Id)).ToList();

                // Check if this is a split-drag (dragging from section handle to unlink)
                bool isSplitDrag = e.Data.GetDataPresent("SplitFromGroup") && e.Data.GetData("SplitFromGroup") is bool split && split;

                // If split-drag, unlink the dragged item
                if (isSplitDrag && droppedItems.Count == 1)
                {
                    droppedItems[0].LinkedGroupId = null;
                }

                OrderItem? target = null;
                if (sender is FrameworkElement fe && fe.DataContext is OrderItem ti) target = ti;
                if (droppedItems.Count > 0)
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                    {
                        await vm.LinkItemsAsync(droppedItems, target);
                        vm.StatusMessage = "Linked items";
                    }
                    else
                    {
                        await vm.MoveOrdersAsync(droppedItems, target);
                        if (isSplitDrag)
                        {
                            vm.StatusMessage = "Split and moved order";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Widget drop failed");
        }
    }

    private void OpenSpotify()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Spotify");
            if (processes.Length > 0)
            {
                // Spotify is running, bring to foreground
                var hWnd = processes[0].MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hWnd);
                    ShowWindow(hWnd, 9); // SW_RESTORE
                }
            }
            else
            {
                // Launch Spotify
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "spotify:",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open Spotify");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_spotifyService != null)
        {
            await _spotifyService.PlayPauseAsync();
        }
    }

    private async void NextTrack_Click(object sender, RoutedEventArgs e)
    {
        if (_spotifyService != null)
        {
            await _spotifyService.NextTrackAsync();
        }
    }

    private async void PrevTrack_Click(object sender, RoutedEventArgs e)
    {
        if (_spotifyService != null)
        {
            await _spotifyService.PreviousTrackAsync();
        }
    }

    private void AddBlankOrder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            _ = AddBlankOrderAsync(vm);
        }
    }

    private void AddBlankNote_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderLogViewModel vm)
        {
            _ = AddBlankNoteAsync(vm);
        }
    }

    private async Task AddBlankOrderAsync(OrderLogViewModel vm)
    {
        var order = OrderItem.CreateBlankOrder();
        await vm.AddOrderAsync(order);
    }

    private async Task AddBlankNoteAsync(OrderLogViewModel vm)
    {
        var note = OrderItem.CreateBlankNote();
        await vm.AddOrderAsync(note);
    }

    private void ColorBar_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        
        // Only allow color picking for sticky notes - orders use status colors
        if (order.NoteType != NoteType.StickyNote) return;

        var picker = new OrderColorPickerWindow(order.ColorHex)
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true)
        {
            order.ColorHex = picker.SelectedColor;
            _ = vm.SaveAsync();
        }
    }

    private void ChangeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.DataContext is not OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        
        // Only allow color picking for sticky notes - orders use status colors
        if (order.NoteType != NoteType.StickyNote) return;

        var picker = new OrderColorPickerWindow(order.ColorHex)
        {
            Owner = Window.GetWindow(this)
        };

        if (picker.ShowDialog() == true)
        {
            order.ColorHex = picker.SelectedColor;
            _ = vm.SaveAsync();
        }
    }

    private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.DataContext is not OrderItem order) return;
        if (DataContext is not OrderLogViewModel vm) return;
        if (comboBox.SelectedItem is not ComboBoxItem selectedItem) return;

        if (selectedItem.Tag is OrderItem.OrderStatus newStatus && order.Status != newStatus)
        {
            _ = vm.SetStatusAsync(order, newStatus);
        }
    }

    private void UnifiedStatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        if (comboBox.DataContext is not ViewModels.OrderItemGroup group) return;
        if (DataContext is not OrderLogViewModel vm) return;
        if (comboBox.SelectedItem is not ComboBoxItem selectedItem) return;
        if (selectedItem.Tag is not OrderItem.OrderStatus newStatus) return;

        // Apply status to ALL members in the group
        foreach (var member in group.Members)
        {
            if (member.Status != newStatus)
            {
                _ = vm.SetStatusAsync(member, newStatus);
            }
        }
    }

    private void SetStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: OrderItem order, Tag: OrderItem.OrderStatus status } &&
            DataContext is OrderLogViewModel vm)
        {
            _ = vm.SetStatusAsync(order, status);
        }
    }

    private async void LinkWith_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            {
                if (DataContext is OrderLogViewModel vm)
                {
                    var dlg = new LinkOrdersWindow(order, vm) { Owner = Window.GetWindow(this) };
                    if (dlg.ShowDialog() == true)
                    {
                        await vm.SaveAsync();
                        vm.StatusMessage = "Orders linked";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to link orders in widget view");
        }
    }

    private async void Unlink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not OrderLogViewModel vm) return;

            Guid? groupId = null;

            // Handle MenuItem (context menu) with OrderItem DataContext
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            {
                groupId = order.LinkedGroupId;
            }
            // Handle Button (merged card footer) with OrderItemGroup DataContext
            else if (sender is Button button && button.DataContext is ViewModels.OrderItemGroup group)
            {
                groupId = group.LinkedGroupId;
            }
            // Handle FrameworkElement with OrderItemGroup DataContext (generic fallback)
            else if (sender is FrameworkElement fe && fe.DataContext is ViewModels.OrderItemGroup grp)
            {
                groupId = grp.LinkedGroupId;
            }

            if (groupId == null)
            {
                vm.StatusMessage = "Order was not linked";
                return;
            }

            // Clear linked id for all items in same group
            foreach (var item in vm.Items)
            {
                if (item.LinkedGroupId == groupId) item.LinkedGroupId = null;
            }
            foreach (var item in vm.ArchivedItems)
            {
                if (item.LinkedGroupId == groupId) item.LinkedGroupId = null;
            }

            await vm.SaveAsync();
            vm.StatusMessage = "Unlinked group";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to unlink orders in widget view");
        }
    }

    private async void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            {
                if (DataContext is OrderLogViewModel vm)
                {
                    await vm.MoveUpCommand.ExecuteAsync(order);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed MoveUp in widget");
        }
    }

    private async void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
            {
                if (DataContext is OrderLogViewModel vm)
                {
                    await vm.MoveDownCommand.ExecuteAsync(order);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed MoveDown in widget");
        }
    }

    private async void ArchiveNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    order.IsArchived = true;
                    vm.Items.Remove(order);
                    vm.ArchivedItems.Add(order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to archive order");
        }
    }

    private async void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    vm.Items.Remove(order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete order");
        }
    }

    private async void DeleteArchivedNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    vm.ArchivedItems.Remove(order);
                    await vm.SaveAsync();
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete archived order");
        }
    }

    private async void UnarchiveNote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is OrderItem order)
                if (DataContext is OrderLogViewModel vm)
                {
                    await vm.UnarchiveOrderCommand.ExecuteAsync(order);
                }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to unarchive order");
        }
    }

    private void OpenFullView_Click(object sender, RoutedEventArgs e)
    {
        OpenFullViewRequested?.Invoke(this, EventArgs.Empty);
    }

    // Inline editing for order card fields
    private void EditableField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Foreground = Application.Current?.Resources["TextPrimaryBrush"] as Brush ?? Brushes.White;
            tb.Background = Application.Current?.Resources["SurfaceHoverBrush"] as Brush ?? Brushes.Transparent;
            tb.SelectAll();
        }
    }

    private async void EditableField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Background = Brushes.Transparent;
            
            // Use disabled color if empty, secondary otherwise
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Foreground = Application.Current?.Resources["TextDisabledBrush"] as Brush ?? Brushes.Gray;
            }
            else
            {
                tb.Foreground = Application.Current?.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray;
            }
            
            // Save changes
            if (DataContext is OrderLogViewModel vm)
            {
                await vm.SaveAsync();
            }
        }
    }

    #region Text Formatting Tools

    private void FormatBold_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.FormatBold(sender, this);

    private void FormatItalic_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.FormatItalic(sender, this);

    private void FormatUnderline_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.FormatUnderline(sender, this);

    private void InsertBullet_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertBullet(sender, this);

    private void InsertCheckbox_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertCheckbox(sender, this);

    private void InsertTimestamp_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertTimestamp(sender, this);

    private void InsertDivider_Click(object sender, RoutedEventArgs e)
        => Helpers.TextFormattingHelper.InsertDivider(sender, this);

    private void NoteContent_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is RichTextBox rtb)
            Helpers.TextFormattingHelper.HandleListAutoContinuation(rtb, e);
    }

    private void NoteContent_Loaded(object sender, RoutedEventArgs e)
    {
        Helpers.TextFormattingHelper.LoadNoteContent(sender);
    }

    private void NoteContent_LostFocus(object sender, RoutedEventArgs e)
    {
        Helpers.TextFormattingHelper.UpdateNoteContent(sender, this);
    }

    #endregion

    #region Merged Card Drag and Drop

    private System.Windows.Point _mergedCardDragStartPoint;

    private void MergedCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            _mergedCardDragStartPoint = e.GetPosition(null);
        }
    }

    private void MergedCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not ViewModels.OrderItemGroup group) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _mergedCardDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _mergedCardDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        // Drag all member IDs
        var ids = group.Members.Select(m => m.Id).ToArray();
        var data = new DataObject();
        data.SetData("OrderItemIds", ids);
        data.SetData("IsMergedCard", true); // Flag to indicate it's a merged card drag

        DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
    }

    private void MergedCard_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("OrderItemId") || e.Data.GetDataPresent("OrderItemIds"))
        {
            e.Effects = DragDropEffects.Move;

            // Visual feedback
            if (sender is Border b)
            {
                if (b.Tag == null) b.Tag = b.BorderBrush;
                b.BorderBrush = (System.Windows.Media.Brush)Application.Current?.Resources["AccentBrush"] ?? System.Windows.Media.Brushes.LightBlue;
                b.BorderThickness = new Thickness(3);
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void MergedCard_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b && b.Tag is System.Windows.Media.Brush orig)
        {
            b.BorderBrush = orig;
            b.BorderThickness = new Thickness(1);
            b.Tag = null;
        }
    }

    private async void MergedCard_Drop(object sender, DragEventArgs e)
    {
        try
        {
            // Reset visual feedback
            if (sender is Border b && b.Tag is System.Windows.Media.Brush orig)
            {
                b.BorderBrush = orig;
                b.BorderThickness = new Thickness(1);
                b.Tag = null;
            }

            if (!e.Data.GetDataPresent("OrderItemId") && !e.Data.GetDataPresent("OrderItemIds")) return;

            var droppedIds = new System.Collections.Generic.List<Guid>();
            if (e.Data.GetDataPresent("OrderItemIds") && e.Data.GetData("OrderItemIds") is Guid[] arr)
            {
                droppedIds.AddRange(arr);
            }
            else if (e.Data.GetDataPresent("OrderItemId"))
            {
                droppedIds.Add((Guid)e.Data.GetData("OrderItemId"));
            }

            if (DataContext is not OrderLogViewModel vm) return;
            if (sender is not FrameworkElement fe || fe.DataContext is not ViewModels.OrderItemGroup targetGroup) return;

            var droppedItems = vm.Items.Concat(vm.ArchivedItems).Where(i => droppedIds.Contains(i.Id)).ToList();
            var target = targetGroup.First; // Drop before the first item of target group

            // Check if this is a split-drag (dragging from section handle to unlink)
            bool isSplitDrag = e.Data.GetDataPresent("SplitFromGroup") && e.Data.GetData("SplitFromGroup") is bool split && split;

            // If split-drag, unlink the dragged item
            if (isSplitDrag && droppedItems.Count == 1)
            {
                droppedItems[0].LinkedGroupId = null;
            }

            if (droppedItems.Count > 0)
            {
                // If Ctrl is held, link with target group
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    await vm.LinkItemsAsync(droppedItems, target);
                    vm.StatusMessage = "Linked items";
                }
                else
                {
                    await vm.MoveOrdersAsync(droppedItems, target);
                    if (isSplitDrag)
                    {
                        vm.StatusMessage = "Split and moved order";
                    }
                    else
                    {
                        vm.StatusMessage = $"Moved {droppedItems.Count} item(s)";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Merged card drop failed");
        }
    }

    #endregion

    #region Section Drag Handles (Split-Drag)

    private System.Windows.Point _sectionDragStartPoint;

    private void SectionDragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            _sectionDragStartPoint = e.GetPosition(null);
            e.Handled = true; // Prevent merged card drag from starting
        }
    }

    private void SectionDragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not Border border) return;

        // Find the OrderItem from the Border's DataContext
        var current = border.DataContext;
        if (current is not OrderItem orderItem) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _sectionDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _sectionDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        // Drag this single order (will auto-unlink when dropped elsewhere)
        var data = new DataObject();
        data.SetData("OrderItemId", orderItem.Id);
        data.SetData("SplitFromGroup", true); // Flag to indicate split-drag

        DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
        e.Handled = true;
    }

    #endregion

    #region Container Drop Zone (iOS-like behavior)

    private void Container_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("OrderItemId") || e.Data.GetDataPresent("OrderItemIds"))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void Container_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent("OrderItemId") && !e.Data.GetDataPresent("OrderItemIds")) return;

            var droppedIds = new System.Collections.Generic.List<Guid>();
            if (e.Data.GetDataPresent("OrderItemIds") && e.Data.GetData("OrderItemIds") is Guid[] arr)
            {
                droppedIds.AddRange(arr);
            }
            else if (e.Data.GetDataPresent("OrderItemId"))
            {
                droppedIds.Add((Guid)e.Data.GetData("OrderItemId"));
            }

            if (DataContext is not OrderLogViewModel vm) return;

            var droppedItems = vm.Items.Concat(vm.ArchivedItems).Where(i => droppedIds.Contains(i.Id)).ToList();

            // Check if this is a split-drag (dragging from section handle to unlink)
            bool isSplitDrag = e.Data.GetDataPresent("SplitFromGroup") && e.Data.GetData("SplitFromGroup") is bool split && split;

            // If split-drag, unlink the dragged item
            if (isSplitDrag && droppedItems.Count == 1)
            {
                droppedItems[0].LinkedGroupId = null;
                await vm.SaveAsync();
                vm.StatusMessage = "Unlinked order";
            }
            else
            {
                // Just moved to empty space - keep current position
                vm.StatusMessage = $"Moved {droppedItems.Count} item(s)";
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Container drop failed");
        }
    }

    #endregion
}
