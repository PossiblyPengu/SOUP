using System;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SOUP.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SOUP.ViewModels;

/// <summary>
/// Launcher ViewModel for navigating to different modules
/// </summary>
public partial class LauncherViewModel : ViewModelBase, IDisposable
{
    private readonly ThemeService _themeService;
    private readonly NavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LauncherViewModel>? _logger;
    private readonly ModuleConfiguration _moduleConfig;
    private readonly WidgetThreadService? _widgetThreadService;
    private bool _disposed;

    [ObservableProperty]
    private bool _isDarkMode;

    /// <summary>
    /// Whether the AllocationBuddy module is enabled
    /// </summary>
    public bool IsAllocationBuddyEnabled => _moduleConfig.AllocationBuddyEnabled;

    /// <summary>
    /// Whether the EssentialsBuddy module is enabled
    /// </summary>
    public bool IsEssentialsBuddyEnabled => _moduleConfig.EssentialsBuddyEnabled;

    /// <summary>
    /// Whether the ExpireWise module is enabled
    /// </summary>
    public bool IsExpireWiseEnabled => _moduleConfig.ExpireWiseEnabled;

    /// <summary>
    /// Whether the SwiftLabel module is enabled
    /// </summary>
    public bool IsSwiftLabelEnabled => _moduleConfig.SwiftLabelEnabled;

    /// <summary>
    /// Whether the OrderLog module is enabled
    /// </summary>
    public bool IsOrderLogEnabled => _moduleConfig.OrderLogEnabled;

    public LauncherViewModel(
        ThemeService themeService,
        NavigationService navigationService,
        IServiceProvider serviceProvider,
        ILogger<LauncherViewModel>? logger = null,
        WidgetThreadService? widgetThreadService = null)
    {
        _themeService = themeService;
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _moduleConfig = ModuleConfiguration.Instance;
        _widgetThreadService = widgetThreadService;

        // Initialize dark mode state
        _isDarkMode = _themeService.IsDarkMode;

        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        _logger?.LogInformation("Module configuration: AllocationBuddy={AB}, EssentialsBuddy={EB}, ExpireWise={EW}, SwiftLabel={SL}, OrderLog={OL}",
            IsAllocationBuddyEnabled, IsEssentialsBuddyEnabled, IsExpireWiseEnabled, IsSwiftLabelEnabled, IsOrderLogEnabled);
    }

    private void OnThemeChanged(object? sender, bool isDark)
    {
        IsDarkMode = isDark;
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
                _themeService.ThemeChanged -= OnThemeChanged;
            }
            _disposed = true;
        }
    }

    [RelayCommand]
    private void LaunchExpireWise()
    {
        if (!IsExpireWiseEnabled)
        {
            _logger?.LogWarning("Attempted to launch disabled module: ExpireWise");
            return;
        }
        _logger?.LogInformation("Navigating to ExpireWise module");
        EnsureMainWindowVisible();
        var viewModel = _serviceProvider.GetRequiredService<ExpireWiseViewModel>();
        _navigationService.NavigateToModule("ExpireWise", viewModel);
    }

    [RelayCommand]
    private void LaunchAllocationBuddy()
    {
        if (!IsAllocationBuddyEnabled)
        {
            _logger?.LogWarning("Attempted to launch disabled module: AllocationBuddy");
            return;
        }
        _logger?.LogInformation("Navigating to AllocationBuddy RPG module");
        EnsureMainWindowVisible();
        var viewModel = _serviceProvider.GetRequiredService<AllocationBuddyRPGViewModel>();
        _navigationService.NavigateToModule("AllocationBuddy", viewModel);
    }

    [RelayCommand]
    private void LaunchEssentialsBuddy()
    {
        if (!IsEssentialsBuddyEnabled)
        {
            _logger?.LogWarning("Attempted to launch disabled module: EssentialsBuddy");
            return;
        }
        _logger?.LogInformation("Navigating to EssentialsBuddy module");
        EnsureMainWindowVisible();
        var viewModel = _serviceProvider.GetRequiredService<EssentialsBuddyViewModel>();
        _navigationService.NavigateToModule("EssentialsBuddy", viewModel);
    }

    [RelayCommand]
    private void LaunchSwiftLabel()
    {
        if (!IsSwiftLabelEnabled)
        {
            _logger?.LogWarning("Attempted to launch disabled module: SwiftLabel");
            return;
        }
        _logger?.LogInformation("Navigating to SwiftLabel module");
        EnsureMainWindowVisible();
        var viewModel = _serviceProvider.GetRequiredService<SwiftLabelViewModel>();
        _navigationService.NavigateToModule("SwiftLabel", viewModel);
    }

    [RelayCommand]
    private async Task LaunchOrderLogAsync()
    {
        if (!IsOrderLogEnabled)
        {
            _logger?.LogWarning("Attempted to launch disabled module: OrderLog");
            return;
        }
        _logger?.LogInformation("Navigating to OrderLog module");
        EnsureMainWindowVisible();
        var viewModel = _serviceProvider.GetRequiredService<Features.OrderLog.ViewModels.OrderLogViewModel>();
        await viewModel.InitializeAsync();
        _navigationService.NavigateToModule("OrderLog", viewModel);
    }

    /// <summary>
    /// Ensures the MainWindow is visible and activated (for when it's hidden due to widget mode)
    /// </summary>
    private static void EnsureMainWindowVisible()
    {
        if (Application.Current?.MainWindow is { } mainWindow && !mainWindow.IsVisible)
        {
            mainWindow.Show();
            mainWindow.Activate();
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _logger?.LogInformation("Toggling theme. Current: {IsDarkMode}", _themeService.IsDarkMode);
        _themeService.ToggleTheme();
    }

    // Pop-out commands to open modules in separate windows
    [RelayCommand]
    private void PopOutExpireWise()
    {
        if (!IsExpireWiseEnabled)
        {
            _logger?.LogWarning("Attempted to pop out disabled module: ExpireWise");
            return;
        }
        _logger?.LogInformation("Opening ExpireWise in new window");
        var viewModel = _serviceProvider.GetRequiredService<ExpireWiseViewModel>();
        var window = new Windows.ExpireWiseWindow(viewModel);
        // Only set owner if MainWindow is visible (not hidden due to widget mode)
        if (Application.Current?.MainWindow is { } mainWindow && mainWindow.IsVisible)
            window.Owner = mainWindow;
        window.Show();
    }

    [RelayCommand]
    private void PopOutAllocationBuddy()
    {
        if (!IsAllocationBuddyEnabled)
        {
            _logger?.LogWarning("Attempted to pop out disabled module: AllocationBuddy");
            return;
        }
        _logger?.LogInformation("Opening AllocationBuddy RPG in new window");
        var viewModel = _serviceProvider.GetRequiredService<AllocationBuddyRPGViewModel>();
        var window = new Windows.AllocationBuddyWindow(viewModel);
        // Only set owner if MainWindow is visible (not hidden due to widget mode)
        if (Application.Current?.MainWindow is { } mainWindow && mainWindow.IsVisible)
            window.Owner = mainWindow;
        window.Show();
    }

    [RelayCommand]
    private void PopOutEssentialsBuddy()
    {
        if (!IsEssentialsBuddyEnabled)
        {
            _logger?.LogWarning("Attempted to pop out disabled module: EssentialsBuddy");
            return;
        }
        _logger?.LogInformation("Opening EssentialsBuddy in new window");
        var viewModel = _serviceProvider.GetRequiredService<EssentialsBuddyViewModel>();
        var window = new Windows.EssentialsBuddyWindow(viewModel);
        // Only set owner if MainWindow is visible (not hidden due to widget mode)
        if (Application.Current?.MainWindow is { } mainWindow && mainWindow.IsVisible)
            window.Owner = mainWindow;
        window.Show();
    }

    [RelayCommand]
    private void PopOutSwiftLabel()
    {
        if (!IsSwiftLabelEnabled)
        {
            _logger?.LogWarning("Attempted to pop out disabled module: SwiftLabel");
            return;
        }
        _logger?.LogInformation("Opening SwiftLabel in new window");
        var viewModel = _serviceProvider.GetRequiredService<SwiftLabelViewModel>();
        var window = new Windows.SwiftLabelWindow(viewModel);
        // Only set owner if MainWindow is visible (not hidden due to widget mode)
        if (Application.Current?.MainWindow is { } mainWindow && mainWindow.IsVisible)
            window.Owner = mainWindow;
        window.Show();
    }

    [RelayCommand]
    private void OpenOrderLogWidget()
    {
        if (!IsOrderLogEnabled)
        {
            _logger?.LogWarning("Attempted to open disabled module widget: OrderLog");
            return;
        }
        _logger?.LogInformation("Opening OrderLog widget on separate thread");
        
        // Use the WidgetThreadService to open widget on its own thread
        // This makes it independent from modal dialogs in the main app
        if (_widgetThreadService != null)
        {
            _widgetThreadService.ShowOrderLogWidget();
        }
        else
        {
            // Fallback to same-thread widget if service not available
            var widget = _serviceProvider.GetRequiredService<Windows.OrderLogWidgetWindow>();
            widget.ShowWidget();
        }
    }

}

