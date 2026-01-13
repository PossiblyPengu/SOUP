using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SOUP.Services;
using SOUP.ViewModels;

namespace SOUP;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Main launcher window with sidebar navigation and custom chrome
/// </summary>
public partial class MainWindow : Window
{
    private const string WindowKey = "MainWindow";
    private readonly WidgetProcessService? _widgetProcessService;
    private readonly TrayIconService? _trayIconService;
    private bool _hasBeenShownOnce;

    #region Win32 Interop for WorkArea-aware maximize

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    #endregion

    public MainWindow(
        MainWindowViewModel viewModel,
        WidgetProcessService? widgetProcessService = null,
        TrayIconService? trayIconService = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _widgetProcessService = widgetProcessService;
        _trayIconService = trayIconService;

        // Attach window position/size persistence
        WindowSettingsService.Instance.AttachToWindow(this, WindowKey);

        // Handle closing to check if widget or close-to-tray should keep app alive
        Closing += MainWindow_Closing;

        // Handle visibility changes to refresh bindings when window is shown again
        IsVisibleChanged += MainWindow_IsVisibleChanged;

        // Hook into WM_GETMINMAXINFO to respect WorkArea when maximized
        SourceInitialized += MainWindow_SourceInitialized;

        // Setup tray icon events
        if (_trayIconService != null)
        {
            _trayIconService.ShowRequested += OnTrayShowRequested;
            _trayIconService.ExitRequested += OnTrayExitRequested;
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            // Get current monitor's work area (excludes taskbar and AppBars)
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var workArea = monitorInfo.rcWork;
                    var monitorArea = monitorInfo.rcMonitor;

                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                    // Set max size to work area size
                    mmi.ptMaxSize.X = workArea.Right - workArea.Left;
                    mmi.ptMaxSize.Y = workArea.Bottom - workArea.Top;

                    // Set max position to work area top-left (relative to monitor)
                    mmi.ptMaxPosition.X = workArea.Left - monitorArea.Left;
                    mmi.ptMaxPosition.Y = workArea.Top - monitorArea.Top;

                    Marshal.StructureToPtr(mmi, lParam, true);
                }
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            // Skip first show - only handle re-shows after being hidden
            if (!_hasBeenShownOnce)
            {
                _hasBeenShownOnce = true;
                return;
            }

            // Window became visible again after being hidden
            if (DataContext is MainWindowViewModel vm)
            {
                // If a module view was open when hidden, go back to launcher
                if (vm.NavigationService.CurrentView != null)
                {
                    vm.NavigationService.NavigateToLauncher();
                }

                // Refresh command bindings
                vm.OpenSettingsCommand.NotifyCanExecuteChanged();
                vm.ToggleThemeCommand.NotifyCanExecuteChanged();
                vm.ShowAboutCommand.NotifyCanExecuteChanged();
            }
            else
            {
                Serilog.Log.Warning("MainWindow DataContext was null when window became visible");
            }
        }
    }

    private void OnTrayShowRequested()
    {
        // Ensure we're on the UI thread (tray events come from WinForms thread)
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnTrayShowRequested);
            return;
        }

        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnTrayExitRequested()
    {
        // Force close - bypass the closing handler's hide logic
        _trayIconService?.Dispose();
        Application.Current.Shutdown();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // If updating, bypass all closing logic
        if (App.IsUpdating)
        {
            return;
        }

        // Check settings
        var closeToTray = _trayIconService?.CloseToTray == true;
        var confirmBeforeExit = _trayIconService?.ConfirmBeforeExit == true;
        var keepWidgetRunning = _trayIconService?.KeepWidgetRunning ?? true;

        // Check if the Order Log widget is still open (runs as separate process)
        var widgetOpen = _widgetProcessService?.IsWidgetOpen == true;

        // If close-to-tray enabled, or widget is open and we want to keep it running, hide instead of close
        if (closeToTray || (widgetOpen && keepWidgetRunning))
        {
            e.Cancel = true;
            this.Hide();

            // Show balloon tip when minimizing to tray (only if no widget keeping it alive)
            if (closeToTray && !widgetOpen)
            {
                _trayIconService?.ShowBalloon(
                    "S.O.U.P",
                    "Application minimized to tray. Double-click to restore.",
                    System.Windows.Forms.ToolTipIcon.Info,
                    2000);
            }

            // If widget is not open and we're hiding, we should dispose tray if ShowTrayIcon is false
            // This handles the case where user disabled tray icon in settings
            if (!widgetOpen && _trayIconService?.ShowTrayIcon == false)
            {
                _trayIconService?.Dispose();
            }
        }
        else
        {
            // Check if we should confirm before exit
            if (confirmBeforeExit)
            {
                if (!Windows.ConfirmExitDialog.ShowDialog(this))
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Close widget if it's running and KeepWidgetRunning is false
            if (widgetOpen && !keepWidgetRunning)
            {
                _widgetProcessService?.KillWidget();
            }

            // Shut down the application
            _trayIconService?.Dispose();
            Application.Current.Shutdown();
        }
    }

    // Custom title bar drag
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            if (WindowState == WindowState.Maximized)
            {
                // Get mouse position relative to window
                var point = e.GetPosition(this);

                // Restore window
                WindowState = WindowState.Normal;

                // Move window so cursor is at the same relative position
                Left = point.X - (Width / 2);
                Top = point.Y - 20;
            }
            DragMove();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Serilog.Log.Information("SettingsButton_Click fired");

        // Fallback click handler in case command binding fails after window hide/show
        if (DataContext is MainWindowViewModel vm)
        {
            Serilog.Log.Information("DataContext is valid, executing OpenSettings");
            vm.OpenSettingsCommand.Execute(null);
        }
        else
        {
            Serilog.Log.Warning("SettingsButton_Click: DataContext is not MainWindowViewModel, it is {Type}", DataContext?.GetType().Name ?? "null");
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
