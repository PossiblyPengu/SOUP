using System.Windows;
using SAP.ViewModels;

namespace SAP;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Main launcher window with card-based navigation
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Wire up theme toggle icon visibility in code-behind
        // (WPF doesn't have built-in boolean negation in binding like Avalonia)
        if (viewModel.LauncherViewModel != null)
        {
            viewModel.LauncherViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LauncherViewModel.IsDarkMode))
                {
                    UpdateThemeIcon(viewModel.LauncherViewModel.IsDarkMode);
                }
            };

            // Set initial state
            UpdateThemeIcon(viewModel.LauncherViewModel.IsDarkMode);
        }
    }

    private void UpdateThemeIcon(bool isDarkMode)
    {
        if (MoonIcon != null && SunIcon != null)
        {
            MoonIcon.Visibility = isDarkMode ? Visibility.Collapsed : Visibility.Visible;
            SunIcon.Visibility = isDarkMode ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
