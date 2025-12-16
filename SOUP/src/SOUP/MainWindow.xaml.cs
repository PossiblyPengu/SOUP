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

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Attach window position/size persistence
        WindowSettingsService.Instance.AttachToWindow(this, WindowKey);
        
        // Handle closing to check if widget should keep app alive
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Check if the Order Log widget is still open
        var widgetOpen = Application.Current.Windows
            .OfType<Windows.OrderLogWidgetWindow>()
            .Any(w => w.IsVisible);
        
        // If widget is not open, shutdown the app
        if (!widgetOpen)
        {
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
