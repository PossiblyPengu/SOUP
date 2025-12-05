using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAP.Services;
using SAP.Views;
using SAP.Windows;

namespace SAP.ViewModels;

/// <summary>
/// Main window ViewModel - Launcher with module navigation
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindowViewModel>? _logger;
    private readonly ThemeService _themeService;
    private bool _disposed;

    [ObservableProperty]
    private string _title = "S.A.P";

    [ObservableProperty]
    private bool _isDarkMode = true;

    public LauncherViewModel LauncherViewModel { get; }
    public NavigationService NavigationService { get; }

    public MainWindowViewModel(
        LauncherViewModel launcherViewModel,
        NavigationService navigationService,
        ThemeService themeService,
        IServiceProvider serviceProvider,
        ILogger<MainWindowViewModel>? logger = null)
    {
        LauncherViewModel = launcherViewModel;
        NavigationService = navigationService;
        _themeService = themeService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Sync with theme service
        IsDarkMode = _themeService.IsDarkMode;
        _themeService.ThemeChanged += OnThemeChanged;

        // Update title when navigating to modules
        NavigationService.ModuleChanged += OnModuleChanged;
    }

    private void OnThemeChanged(object? sender, bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
    }

    private void OnModuleChanged(object? sender, string moduleName)
    {
        Title = moduleName == "Launcher"
            ? "S.A.P"
            : $"S.A.P - {moduleName}";
    }

    /// <summary>
    /// Toggles between light and dark themes (Ctrl+T shortcut).
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        _logger?.LogInformation("Theme toggled to {Theme}", IsDarkMode ? "Dark" : "Light");
    }

    /// <summary>
    /// Shows the About dialog with version and module information (F1 shortcut).
    /// </summary>
    [RelayCommand]
    private void ShowAbout()
    {
        try
        {
            var aboutWindow = new AboutWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
            _logger?.LogInformation("Opened About dialog");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open About dialog");
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsViewModel = _serviceProvider.GetRequiredService<UnifiedSettingsViewModel>();
            var settingsWindow = new UnifiedSettingsWindow(settingsViewModel);
            settingsWindow.ShowDialog();
            _logger?.LogInformation("Opened unified settings window");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open unified settings window");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Unsubscribe from events to prevent memory leaks
                NavigationService.ModuleChanged -= OnModuleChanged;
                _themeService.ThemeChanged -= OnThemeChanged;
            }
            _disposed = true;
        }
    }
}
