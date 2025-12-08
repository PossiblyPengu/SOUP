using System;
using System.Windows;
using System.Windows.Input;
using SAP.ViewModels;
using SAP.Helpers;
using SAP.Services;

namespace SAP.Windows;

public partial class EssentialsBuddyWindow : Window
{
    private const string WindowKey = "EssentialsBuddyWindow";

    public EssentialsBuddyWindow(EssentialsBuddyViewModel viewModel)
    {
        // Apply theme BEFORE InitializeComponent so DynamicResources can resolve
        if (ThemeService.Instance.IsWindows95Mode)
            ApplyTheme(false); // Always use Win98 theme in easter egg mode
        else
            ApplyTheme(ThemeService.Instance.IsDarkMode);

        InitializeComponent();
        DataContext = viewModel;

        // Attach window position/size persistence
        WindowSettingsService.Instance.AttachToWindow(this, WindowKey);

        // Subscribe to theme changes
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
        ThemeService.Instance.Windows95ModeChanged += OnWindows95ModeChanged;

        // Enable smooth window opening animation
        Loaded += (s, e) =>
        {
            WindowAnimationHelper.AnimateWindowOpen(this);
            UpdateThemeIcon(ThemeService.Instance.IsDarkMode);
        };
        Closed += (s, e) => {
            ThemeService.Instance.ThemeChanged -= OnThemeChanged;
            ThemeService.Instance.Windows95ModeChanged -= OnWindows95ModeChanged;
        };
    }

    private void OnWindows95ModeChanged(object? sender, bool isWin98)
    {
        ApplyTheme(isWin98 ? false : ThemeService.Instance.IsDarkMode);
        UpdateThemeIcon(ThemeService.Instance.IsDarkMode);
    }

    private void OnThemeChanged(object? sender, bool isDarkMode)
    {
        ApplyTheme(isDarkMode);
        UpdateThemeIcon(isDarkMode);
    }

    private void UpdateThemeIcon(bool isDarkMode)
    {
        if (ThemeToggleButton?.Template?.FindName("ThemeIcon", ThemeToggleButton) is System.Windows.Controls.TextBlock icon)
        {
            icon.Text = isDarkMode ? "🌙" : "☀️";
        }
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.Instance.ToggleTheme();
    }

    private void ApplyTheme(bool isDarkMode)
    {
        var themePath = isDarkMode
            ? "pack://application:,,,/Themes/DarkTheme.xaml"
            : "pack://application:,,,/Themes/LightTheme.xaml";

        var themeDict = new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Absolute)
        };

        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(themeDict);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
