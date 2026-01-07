using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
    private readonly WidgetThreadService? _widgetThreadService;
    private readonly TrayIconService? _trayIconService;
    private bool _hasBeenShownOnce;

    public MainWindow(
        MainWindowViewModel viewModel, 
        WidgetThreadService? widgetThreadService = null,
        TrayIconService? trayIconService = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _widgetThreadService = widgetThreadService;
        _trayIconService = trayIconService;

        // Attach window position/size persistence
        WindowSettingsService.Instance.AttachToWindow(this, WindowKey);
        
        // Handle closing to check if widget or close-to-tray should keep app alive
        Closing += MainWindow_Closing;
        
        // Handle visibility changes to refresh bindings when window is shown again
        IsVisibleChanged += MainWindow_IsVisibleChanged;

        // Setup tray icon events
        if (_trayIconService != null)
        {
            _trayIconService.ShowRequested += OnTrayShowRequested;
            _trayIconService.ExitRequested += OnTrayExitRequested;
        }
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
        // Check settings
        var closeToTray = _trayIconService?.CloseToTray == true;
        var confirmBeforeExit = _trayIconService?.ConfirmBeforeExit == true;
        var keepWidgetRunning = _trayIconService?.KeepWidgetRunning ?? true;
        
        // Check if the Order Log widget is still open (runs on separate thread)
        var widgetOpen = _widgetThreadService?.IsWidgetOpen == true;
        
        // Also check same-thread widgets as fallback
        if (!widgetOpen)
        {
            widgetOpen = Application.Current.Windows
                .OfType<Windows.OrderLogWidgetWindow>()
                .Any(w => w != null && w.IsVisible);
        }
        
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
            
            // No widget open and no close-to-tray, shut down the application
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
