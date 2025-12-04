using System.Windows;
using System.Windows.Input;
using SAP.ViewModels;

namespace SAP;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Main launcher window with sidebar navigation and custom chrome
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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
