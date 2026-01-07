using System.Windows;
using SOUP.Services;

namespace SOUP.Windows;

/// <summary>
/// Themed confirmation dialog for exiting the application
/// </summary>
public partial class ConfirmExitDialog : Window
{
    public ConfirmExitDialog()
    {
        InitializeComponent();
        
        // Apply current theme
        ApplyTheme();
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
        
        Unloaded += (s, e) => ThemeService.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, bool isDarkMode)
    {
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var themePath = ThemeService.Instance.IsDarkMode
            ? "pack://application:,,,/SOUP;component/Themes/DarkTheme.xaml"
            : "pack://application:,,,/SOUP;component/Themes/LightTheme.xaml";

        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri(themePath) });
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Shows the dialog and returns true if user confirms exit
    /// </summary>
    public static bool ShowDialog(Window? owner)
    {
        var dialog = new ConfirmExitDialog();
        
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        
        return dialog.ShowDialog() == true;
    }
}
