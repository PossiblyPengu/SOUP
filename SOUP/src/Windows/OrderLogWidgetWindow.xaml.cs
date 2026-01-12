using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Services;

namespace SOUP.Windows;

/// <summary>
/// AppBar widget window for Order Log that docks to screen edges and reserves screen space
/// </summary>
public partial class OrderLogWidgetWindow : Window
{
    private readonly OrderLogViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    
    // AppBar state
    private bool _isAppBarRegistered;
    private AppBarEdge _currentEdge = AppBarEdge.None;
    private AppBarEdge _edgeBeforeMinimize = AppBarEdge.None;
    private int _appBarCallbackId;
    private HwndSource? _hwndSource;
    
    // Default width for the docked appbar
    private readonly int _dockedWidth = 380;
    
    // Update checking
    private Timer? _updateCheckTimer;
    private UpdateInfo? _availableUpdate;
    private CancellationTokenSource? _updateCheckCts;

    #region Windows API Imports
    
    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern int RegisterWindowMessage(string msg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const uint ABM_NEW = 0x00;
    private const uint ABM_REMOVE = 0x01;
    private const uint ABM_QUERYPOS = 0x02;
    private const uint ABM_SETPOS = 0x03;
    private const uint ABM_GETSTATE = 0x04;
    private const uint ABM_GETTASKBARPOS = 0x05;
    private const uint ABM_ACTIVATE = 0x06;
    private const uint ABM_GETAUTOHIDEBAR = 0x07;
    private const uint ABM_SETAUTOHIDEBAR = 0x08;
    private const uint ABM_WINDOWPOSCHANGED = 0x09;

    private const uint ABN_STATECHANGE = 0x00;
    private const uint ABN_POSCHANGED = 0x01;
    private const uint ABN_FULLSCREENAPP = 0x02;
    private const uint ABN_WINDOWARRANGE = 0x03;

    private const uint ABE_LEFT = 0;
    private const uint ABE_TOP = 1;
    private const uint ABE_RIGHT = 2;
    private const uint ABE_BOTTOM = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private enum AppBarEdge
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    #endregion

    public OrderLogWidgetWindow(OrderLogViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        
        WidgetView.DataContext = _viewModel;
        
        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        
        ApplyTheme();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
        
        // Make the widget window not participate in modal dialog blocking
        // WS_EX_TOOLWINDOW prevents it from being disabled when modal dialogs open
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        
        // Register callback message
        _appBarCallbackId = RegisterWindowMessage("OrderLogAppBarCallback");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _appBarCallbackId)
        {
            switch (wParam.ToInt32())
            {
                case (int)ABN_POSCHANGED:
                    // Another appbar or the taskbar has changed position
                    if (_isAppBarRegistered && _currentEdge != AppBarEdge.None)
                    {
                        PositionAppBar();
                    }
                    handled = true;
                    break;
                    
                case (int)ABN_FULLSCREENAPP:
                    // A fullscreen app is opening/closing
                    if (lParam.ToInt32() != 0)
                    {
                        // Fullscreen app opening - hide
                        Topmost = false;
                    }
                    else
                    {
                        // Fullscreen app closing - show
                        Topmost = true;
                    }
                    handled = true;
                    break;
            }
        }
        
        return IntPtr.Zero;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Start docked to right edge by default
        DockToEdge(AppBarEdge.Right);
        
        // Add a "+" overlay badge to the taskbar icon to differentiate from main app
        ApplyTaskbarOverlay();

        // Initialize asynchronously without blocking
        InitializeWidgetAsync();
    }
    
    /// <summary>
    /// Applies a "+" overlay badge to the taskbar icon to differentiate the widget from the main app.
    /// </summary>
    private void ApplyTaskbarOverlay()
    {
        try
        {
            // Create a simple "+" badge using DrawingImage
            var drawing = new GeometryDrawing
            {
                Brush = Brushes.White,
                Pen = new Pen(Brushes.Black, 0.5),
                Geometry = new GeometryGroup
                {
                    Children =
                    {
                        // Circle background
                        new EllipseGeometry(new Point(8, 8), 7.5, 7.5),
                    }
                }
            };
            
            var plusDrawing = new GeometryDrawing
            {
                Brush = new SolidColorBrush(Color.FromRgb(139, 92, 246)), // Accent purple
                Geometry = new GeometryGroup
                {
                    Children =
                    {
                        // Horizontal bar of +
                        new RectangleGeometry(new Rect(4, 6.5, 8, 3)),
                        // Vertical bar of +
                        new RectangleGeometry(new Rect(6.5, 4, 3, 8)),
                    }
                }
            };
            
            var drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(drawing);
            drawingGroup.Children.Add(plusDrawing);
            
            var drawingImage = new DrawingImage(drawingGroup);
            drawingImage.Freeze();
            
            TaskbarItemInfo ??= new TaskbarItemInfo();
            TaskbarItemInfo.Overlay = drawingImage;
            TaskbarItemInfo.Description = "Order Log Widget";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply taskbar overlay");
        }
    }

    private async void InitializeWidgetAsync()
    {
        try
        {
            await _viewModel.InitializeAsync();
            
            // Start update check timer (check every 30 minutes, first check after 5 seconds)
            _updateCheckCts = new CancellationTokenSource();
            _updateCheckTimer = new Timer(CheckForUpdatesCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize OrderLog widget");
        }
    }
    
    private async void CheckForUpdatesCallback(object? state)
    {
        try
        {
            // Check if we should cancel
            if (_updateCheckCts?.IsCancellationRequested == true)
                return;
                
            var updateService = _serviceProvider.GetService<UpdateService>();
            if (updateService == null) return;
            
            var updateInfo = await updateService.CheckForUpdatesAsync(_updateCheckCts?.Token ?? CancellationToken.None);
            
            // Check again after async operation
            if (_updateCheckCts?.IsCancellationRequested == true)
                return;
            
            // Update UI on the dispatcher thread
            await Dispatcher.InvokeAsync(() =>
            {
                _availableUpdate = updateInfo;
                if (updateInfo != null)
                {
                    UpdateVersionText.Text = $"v{updateInfo.Version}";
                    UpdateBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    UpdateBadge.Visibility = Visibility.Collapsed;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates in widget");
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Cancel and stop update check timer first
        _updateCheckCts?.Cancel();
        _updateCheckTimer?.Dispose();
        _updateCheckTimer = null;
        _updateCheckCts?.Dispose();
        _updateCheckCts = null;
        
        // Mark thread as background immediately so it won't block process exit
        if (_isRunningOnSeparateThread && Thread.CurrentThread.IsAlive)
        {
            Thread.CurrentThread.IsBackground = true;
        }
        
        // Unregister AppBar before closing
        if (_isAppBarRegistered)
        {
            UnregisterAppBar();
        }
        
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
        
        // If running on separate thread, just close - don't touch Application.Current
        // The OnWidgetClosed event will handle app shutdown if needed
        if (_isRunningOnSeparateThread)
        {
            OnWidgetClosed?.Invoke();
            return;
        }
        
        // Check if we're running as a separate process (launched with --widget)
        var isWidgetProcess = Environment.GetCommandLineArgs().Any(arg => 
            arg.Equals("--widget", StringComparison.OrdinalIgnoreCase));
        
        if (isWidgetProcess)
        {
            // We're a separate widget process - check if main app is still running
            try
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var allSoupProcesses = System.Diagnostics.Process.GetProcessesByName("SOUP");
                var otherProcesses = allSoupProcesses.Where(p => p.Id != currentProcess.Id).ToList();
                
                // If there are other SOUP processes, check if any have visible windows
                bool mainAppVisible = false;
                foreach (var process in otherProcesses)
                {
                    try
                    {
                        // If the process has a main window that's visible, the main app is running
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            mainAppVisible = true;
                            break;
                        }
                    }
                    catch { }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                // If no main app window is visible, kill all other SOUP processes
                if (!mainAppVisible && otherProcesses.Count > 0)
                {
                    Log.Information("Widget closing and main window not visible, terminating all SOUP processes");
                    foreach (var process in allSoupProcesses)
                    {
                        if (process.Id != currentProcess.Id)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to kill process {ProcessId}", process.Id);
                            }
                        }
                        process.Dispose();
                    }
                }
                else
                {
                    // Clean up disposed processes
                    foreach (var p in allSoupProcesses)
                    {
                        p.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking for other SOUP processes");
            }
            
            // Dispose tray icon if we're the last one
            try
            {
                var trayService = _serviceProvider.GetService<TrayIconService>();
                trayService?.Dispose();
            }
            catch { }
            
            return;
        }
        
        // Legacy: Same-process mode - check if MainWindow is visible
        try
        {
            var mainWindowVisible = Application.Current?.Windows
                .OfType<MainWindow>()
                .Any(w => w != null && w.IsVisible) ?? false;
            
            if (!mainWindowVisible)
            {
                // Dispose tray icon before shutdown since both windows are closed
                var trayService = _serviceProvider.GetService<TrayIconService>();
                trayService?.Dispose();
                
                Application.Current?.Shutdown();
            }
        }
        catch
        {
            // Ignore if Application.Current is not accessible
        }
    }
    
    /// <summary>
    /// Event raised when widget is closed (for separate thread mode)
    /// </summary>
    public event Action? OnWidgetClosed;
    
    private bool _isRunningOnSeparateThread;
    
    /// <summary>
    /// Marks this widget as running on a separate thread
    /// </summary>
    public void SetSeparateThreadMode()
    {
        _isRunningOnSeparateThread = true;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && _edgeBeforeMinimize != AppBarEdge.None)
        {
            // Restored from minimized - re-register AppBar at previous edge
            RegisterAppBar();
            _currentEdge = _edgeBeforeMinimize;
            PositionAppBar();
            _edgeBeforeMinimize = AppBarEdge.None;
            
            Log.Debug("AppBar re-registered after restore at edge: {Edge}", _currentEdge);
        }
    }

    #region AppBar Registration

    private void RegisterAppBar()
    {
        if (_isAppBarRegistered) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        var data = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = (uint)_appBarCallbackId
        };

        var result = SHAppBarMessage(ABM_NEW, ref data);
        _isAppBarRegistered = result != 0;
        
        Log.Debug("AppBar registered: {IsRegistered}", _isAppBarRegistered);
    }

    private void UnregisterAppBar()
    {
        if (!_isAppBarRegistered) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        var data = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            hWnd = hwnd
        };

        SHAppBarMessage(ABM_REMOVE, ref data);
        _isAppBarRegistered = false;
        _currentEdge = AppBarEdge.None;
        
        Log.Debug("AppBar unregistered");
    }

    private void PositionAppBar()
    {
        if (!_isAppBarRegistered || _currentEdge == AppBarEdge.None) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
        var workingArea = screen.WorkingArea;
        var screenBounds = screen.Bounds;

        var data = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            hWnd = hwnd,
            uEdge = _currentEdge switch
            {
                AppBarEdge.Left => ABE_LEFT,
                AppBarEdge.Right => ABE_RIGHT,
                AppBarEdge.Top => ABE_TOP,
                AppBarEdge.Bottom => ABE_BOTTOM,
                _ => ABE_RIGHT
            }
        };

        // Calculate desired position based on edge
        int appBarWidth = (int)(_dockedWidth * GetDpiScale());
        int appBarHeight = screenBounds.Height;

        switch (_currentEdge)
        {
            case AppBarEdge.Left:
                data.rc.left = screenBounds.Left;
                data.rc.top = screenBounds.Top;
                data.rc.right = screenBounds.Left + appBarWidth;
                data.rc.bottom = screenBounds.Bottom;
                break;
                
            case AppBarEdge.Right:
                data.rc.left = screenBounds.Right - appBarWidth;
                data.rc.top = screenBounds.Top;
                data.rc.right = screenBounds.Right;
                data.rc.bottom = screenBounds.Bottom;
                break;
        }

        // Query the system for the position
        SHAppBarMessage(ABM_QUERYPOS, ref data);
        
        // Adjust based on query result
        switch (_currentEdge)
        {
            case AppBarEdge.Left:
                data.rc.right = data.rc.left + appBarWidth;
                break;
            case AppBarEdge.Right:
                data.rc.left = data.rc.right - appBarWidth;
                break;
        }

        // Set the position
        SHAppBarMessage(ABM_SETPOS, ref data);

        // Move the window
        var dpiScale = GetDpiScale();
        Left = data.rc.left / dpiScale;
        Top = data.rc.top / dpiScale;
        Width = (data.rc.right - data.rc.left) / dpiScale;
        Height = (data.rc.bottom - data.rc.top) / dpiScale;
        
        // Also use MoveWindow for precision
        MoveWindow(hwnd, data.rc.left, data.rc.top, 
            data.rc.right - data.rc.left, 
            data.rc.bottom - data.rc.top, true);
    }

    private double GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
    }

    #endregion

    #region Dock Commands

    private void DockLeft_Click(object sender, RoutedEventArgs e)
    {
        DockToEdge(AppBarEdge.Left);
    }

    private void DockRight_Click(object sender, RoutedEventArgs e)
    {
        DockToEdge(AppBarEdge.Right);
    }

    private void DockToEdge(AppBarEdge edge)
    {
        // Register as appbar if not already
        if (!_isAppBarRegistered)
        {
            RegisterAppBar();
        }

        _currentEdge = edge;
        
        // Position the appbar
        PositionAppBar();
        
        Log.Debug("Docked to {Edge}", edge);
    }

    #endregion

    #region Theme Support

    private void ApplyTheme()
    {
        try
        {
            var themeService = _serviceProvider.GetService<ThemeService>();
            if (themeService != null)
            {
                ApplyThemeResources(themeService.IsDarkMode);
                themeService.ThemeChanged += OnThemeChanged;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply theme");
        }
    }

    private void ApplyThemeResources(bool isDarkMode)
    {
        var themePath = isDarkMode
            ? "pack://application:,,,/SOUP;component/Themes/DarkTheme.xaml"
            : "pack://application:,,,/SOUP;component/Themes/LightTheme.xaml";

        Resources.MergedDictionaries.Clear();
        
        // Add ModernStyles first (base styles)
        Resources.MergedDictionaries.Add(new ResourceDictionary 
        { 
            Source = new Uri("pack://application:,,,/SOUP;component/Themes/ModernStyles.xaml") 
        });
        
        // Then add color theme
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themePath) });
        
        // Re-apply CardFontSize from ViewModel after theme resources are loaded
        if (_viewModel.CardFontSize > 0)
        {
            Resources["CardFontSize"] = _viewModel.CardFontSize;
        }
    }

    private void OnThemeChanged(object? sender, bool isDarkMode)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                // Invalidate converter caches before applying new theme
                Features.OrderLog.Converters.StatusToColorConverter.InvalidateCache();
                ApplyThemeResources(isDarkMode);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to apply theme change");
            }
        });
    }

    #endregion

    #region Window Event Handlers

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // No dragging - always docked
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // Store current edge position before unregistering
        _edgeBeforeMinimize = _currentEdge;
        
        // Unregister AppBar to release screen space while minimized
        if (_isAppBarRegistered)
        {
            UnregisterAppBar();
        }
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Show the widget window
    /// </summary>
    public void ShowWidget()
    {
        Show();
        Activate();
        
        if (_isAppBarRegistered && _currentEdge != AppBarEdge.None)
        {
            PositionAppBar();
        }
    }
    
    /// <summary>
    /// Opens the unified settings window to the OrderLog tab
    /// </summary>
    public void OpenSettings()
    {
        try
        {
            var settingsViewModel = _serviceProvider.GetRequiredService<ViewModels.UnifiedSettingsViewModel>();
            var settingsWindow = new Views.UnifiedSettingsWindow(settingsViewModel, "orderlog");
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to open settings window from widget");
        }
    }

    /// <summary>
    /// Opens the main S.O.U.P launcher window.
    /// When running as separate process, launches a new instance without --widget flag.
    /// </summary>
    public void OpenLauncher()
    {
        try
        {
            var lifecycleService = ((App)Application.Current)?.GetService<AppLifecycleService>();
            lifecycleService?.OpenMainWindow();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to open launcher from widget");
        }
    }

    private const int SW_RESTORE = 9;
    
    /// <summary>
    /// Handles click on the update badge - performs in-app update
    /// </summary>
    private async void UpdateBadge_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        
        if (_availableUpdate == null) return;

        try
        {
            using var updateService = new UpdateService();
            
            var shouldUpdate = MessageDialog.Show(
                this,
                $"A new version is available!\n\n" +
                $"Current: v{updateService.CurrentVersion}\n" +
                $"Latest: v{_availableUpdate.Version}\n\n" +
                $"{_availableUpdate.ReleaseNotes}\n\n" +
                $"Would you like to download and install it now?",
                "Update Available",
                DialogType.Information,
                DialogButtons.YesNo);

            if (!shouldUpdate) return;

            // Hide badge and show downloading status
            UpdateBadge.Visibility = Visibility.Collapsed;
            _viewModel.StatusMessage = "Downloading update...";

            var progress = new Progress<double>(percent =>
            {
                Dispatcher.Invoke(() =>
                {
                    _viewModel.StatusMessage = $"Downloading... {percent:F0}%";
                });
            });

            var zipPath = await updateService.DownloadUpdateAsync(_availableUpdate, progress);

            if (string.IsNullOrEmpty(zipPath))
            {
                MessageDialog.ShowWarning(this, "Failed to download update. Please try again later.", "Download Failed");
                UpdateBadge.Visibility = Visibility.Visible;
                return;
            }

            _viewModel.StatusMessage = "Applying update...";

            // Apply the update
            if (updateService.ApplyUpdate(zipPath))
            {
                _viewModel.StatusMessage = "Update ready! Restarting...";
                
                // Set global flag to bypass closing confirmations
                App.IsUpdating = true;
                
                // Give the updater script time to start
                await System.Threading.Tasks.Task.Delay(1000);
                
                // Get lifecycle service to force close all processes
                var lifecycleService = ((App)Application.Current)?.GetService<AppLifecycleService>();
                if (lifecycleService != null)
                {
                    await lifecycleService.ForceCloseAllProcessesAsync();
                }
                
                // Give processes time to fully terminate
                await System.Threading.Tasks.Task.Delay(500);
                
                // Shutdown this process gracefully, then force exit
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Application.Current?.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to shutdown application gracefully");
                    }
                });
                
                await System.Threading.Tasks.Task.Delay(500);
                
                // Force exit this process
                Log.Information("Force exiting for update");
                Environment.Exit(0);
            }
            else
            {
                MessageDialog.ShowWarning(this, "Failed to apply update. Please try downloading manually.", "Update Failed");
                UpdateBadge.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update from widget");
            MessageDialog.ShowWarning(this, $"Failed to update: {ex.Message}", "Update Error");
            UpdateBadge.Visibility = Visibility.Visible;
        }
    }

    #endregion
}
