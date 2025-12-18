using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Features.OrderLog.Models;
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
        var order = new OrderItem
        {
            NoteType = NoteType.Order,
            VendorName = string.Empty,
            TransferNumbers = string.Empty,
            WhsShipmentNumbers = string.Empty,
            ColorHex = "#B56576",
            Status = OrderItem.OrderStatus.NotReady
        };

        await vm.AddOrderAsync(order);
    }

    private async Task AddBlankNoteAsync(OrderLogViewModel vm)
    {
        var note = new OrderItem
        {
            NoteType = NoteType.StickyNote,
            NoteTitle = string.Empty,
            NoteContent = string.Empty,
            ColorHex = "#FFD700",
            Status = OrderItem.OrderStatus.OnDeck
        };

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

    private void SetStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: OrderItem order, Tag: OrderItem.OrderStatus status } &&
            DataContext is OrderLogViewModel vm)
        {
            _ = vm.SetStatusAsync(order, status);
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
        if (sender is TextBox textBox)
            Helpers.TextFormattingHelper.HandleListAutoContinuation(textBox, e);
    }

    #endregion
}
