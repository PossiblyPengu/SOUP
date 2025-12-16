using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
    private int _appBarCallbackId;
    private HwndSource? _hwndSource;
    
    // Default width for the docked appbar
    private readonly int _dockedWidth = 380;

    #region Windows API Imports
    
    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern int RegisterWindowMessage(string msg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

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
        WidgetView.OpenFullViewRequested += OnOpenFullViewRequested;
        
        Loaded += OnLoaded;
        Closing += OnClosing;
        SourceInitialized += OnSourceInitialized;
        
        ApplyTheme();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(WndProc);
        
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Start docked to right edge by default
        DockToEdge(AppBarEdge.Right);

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize OrderLog widget");
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Unregister AppBar before closing
        if (_isAppBarRegistered)
        {
            UnregisterAppBar();
        }
        
        WidgetView.OpenFullViewRequested -= OnOpenFullViewRequested;
        
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
        
        // Check if main window is closed - if so, shutdown the app
        var mainWindowOpen = Application.Current.Windows
            .OfType<MainWindow>()
            .Any(w => w.IsVisible);
        
        if (!mainWindowOpen)
        {
            Application.Current.Shutdown();
        }
    }

    #region AppBar Registration

    private void RegisterAppBar()
    {
        if (_isAppBarRegistered) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        var data = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)),
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
            cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)),
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
            cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)),
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
                var themePath = themeService.IsDarkMode
                    ? "pack://application:,,,/SOUP;component/Themes/DarkTheme.xaml"
                    : "pack://application:,,,/SOUP;component/Themes/LightTheme.xaml";

                Resources.MergedDictionaries.Clear();
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themePath) });
                
                themeService.ThemeChanged += OnThemeChanged;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply theme");
        }
    }

    private void OnThemeChanged(object? sender, bool isDarkMode)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var themePath = isDarkMode
                    ? "pack://application:,,,/SOUP;component/Themes/DarkTheme.xaml"
                    : "pack://application:,,,/SOUP;component/Themes/LightTheme.xaml";

                Resources.MergedDictionaries.Clear();
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themePath) });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to apply theme");
            }
        });
    }

    #endregion

    #region Window Event Handlers

    private void OnOpenFullViewRequested(object? sender, EventArgs e)
    {
        try
        {
            var fullWindow = _serviceProvider.GetService<OrderLogWindow>();
            if (fullWindow != null)
            {
                fullWindow.Show();
                fullWindow.Activate();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open full Order Log");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // No dragging - always docked
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
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

    #endregion
}
