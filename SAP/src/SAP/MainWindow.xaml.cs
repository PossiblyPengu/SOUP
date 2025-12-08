using System;
using System.Windows;
using System.Windows.Input;
using SAP.Services;
using SAP.ViewModels;

namespace SAP;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Main launcher window with sidebar navigation and custom chrome
/// </summary>
public partial class MainWindow : Window
{
    private const string WindowKey = "MainWindow";
    private int _versionClickCount = 0;
    private DateTime _lastVersionClick = DateTime.MinValue;
    private const int EasterEggClickThreshold = 7;
    private const int ClickTimeoutSeconds = 3;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Attach window position/size persistence
        WindowSettingsService.Instance.AttachToWindow(this, WindowKey);
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

    /// <summary>
    /// Handles clicks on the version badge for easter egg activation.
    /// </summary>
    private void VersionBadge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        
        // Reset counter if too much time has passed
        if ((now - _lastVersionClick).TotalSeconds > ClickTimeoutSeconds)
        {
            _versionClickCount = 0;
        }
        
        _lastVersionClick = now;
        _versionClickCount++;
        
        if (_versionClickCount >= EasterEggClickThreshold)
        {
            _versionClickCount = 0;
            ActivateWindows95EasterEgg();
        }
    }

    /// <summary>
    /// Activates the Windows 95 easter egg theme.
    /// </summary>
    private void ActivateWindows95EasterEgg()
    {
        var themeService = ThemeService.Instance;
        themeService.ToggleWindows95Mode();
        
        var isEnabled = themeService.IsWindows95Mode;
        var message = isEnabled 
            ? "🖥️ Windows 95 Mode Activated!\n\nWelcome to 1995! Enjoy the retro vibes."
            : "✨ Modern Mode Restored!\n\nWelcome back to the future.";
        
        MessageBox.Show(message, "Easter Egg!", MessageBoxButton.OK, 
            isEnabled ? MessageBoxImage.Information : MessageBoxImage.None);
    }
}
