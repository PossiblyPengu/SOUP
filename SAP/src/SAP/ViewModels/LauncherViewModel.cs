using System;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SAP.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SAP.ViewModels;

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
    /// SwiftLabel is always enabled (core functionality)
    /// </summary>
    public bool IsSwiftLabelEnabled => true;
    /// <summary>
    /// Whether the NotesTracker module is enabled
    /// </summary>
    public bool IsNotesTrackerEnabled => true;

    public LauncherViewModel(
        ThemeService themeService,
        NavigationService navigationService,
        IServiceProvider serviceProvider,
        ILogger<LauncherViewModel>? logger = null)
    {
        _themeService = themeService;
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _moduleConfig = ModuleConfiguration.Instance;

        // Initialize dark mode state
        _isDarkMode = _themeService.IsDarkMode;

        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        _logger?.LogInformation("Module configuration: AllocationBuddy={AB}, EssentialsBuddy={EB}, ExpireWise={EW}",
            IsAllocationBuddyEnabled, IsEssentialsBuddyEnabled, IsExpireWiseEnabled);
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
        _logger?.LogInformation("Navigating to ExpireWise module");
        var viewModel = _serviceProvider.GetRequiredService<ExpireWiseViewModel>();
        _navigationService.NavigateToModule("ExpireWise", viewModel);
    }

    [RelayCommand]
    private void LaunchAllocationBuddy()
    {
        _logger?.LogInformation("Navigating to AllocationBuddy RPG module");
        var viewModel = _serviceProvider.GetRequiredService<AllocationBuddyRPGViewModel>();
        _navigationService.NavigateToModule("AllocationBuddy", viewModel);
    }

    [RelayCommand]
    private void LaunchEssentialsBuddy()
    {
        _logger?.LogInformation("Navigating to EssentialsBuddy module");
        var viewModel = _serviceProvider.GetRequiredService<EssentialsBuddyViewModel>();
        _navigationService.NavigateToModule("EssentialsBuddy", viewModel);
    }

    [RelayCommand]
    private void LaunchSwiftLabel()
    {
        _logger?.LogInformation("Navigating to SwiftLabel module");
        var viewModel = _serviceProvider.GetRequiredService<SwiftLabelViewModel>();
        _navigationService.NavigateToModule("SwiftLabel", viewModel);
    }

    [RelayCommand]
    private void LaunchNotesTracker()
    {
        _logger?.LogInformation("Navigating to NotesTracker module");
        var viewModel = _serviceProvider.GetRequiredService<Features.NotesTracker.ViewModels.NotesTrackerViewModel>();
        _navigationService.NavigateToModule("NotesTracker", viewModel);
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
        _logger?.LogInformation("Opening ExpireWise in new window");
        var viewModel = _serviceProvider.GetRequiredService<ExpireWiseViewModel>();
        var window = new Windows.ExpireWiseWindow(viewModel);
        window.Show();
    }

    [RelayCommand]
    private void PopOutAllocationBuddy()
    {
        _logger?.LogInformation("Opening AllocationBuddy RPG in new window");
        var viewModel = _serviceProvider.GetRequiredService<AllocationBuddyRPGViewModel>();
        var window = new Windows.AllocationBuddyWindow(viewModel);
        window.Show();
    }

    [RelayCommand]
    private void PopOutEssentialsBuddy()
    {
        _logger?.LogInformation("Opening EssentialsBuddy in new window");
        var viewModel = _serviceProvider.GetRequiredService<EssentialsBuddyViewModel>();
        var window = new Windows.EssentialsBuddyWindow(viewModel);
        window.Show();
    }

    [RelayCommand]
    private void PopOutSwiftLabel()
    {
        _logger?.LogInformation("Opening SwiftLabel in new window");
        var viewModel = _serviceProvider.GetRequiredService<SwiftLabelViewModel>();
        var window = new Windows.SwiftLabelWindow(viewModel);
        window.Show();
    }

    [RelayCommand]
    private void PopOutNotesTracker()
    {
        _logger?.LogInformation("Opening NotesTracker in new window");
        var viewModel = _serviceProvider.GetRequiredService<Features.NotesTracker.ViewModels.NotesTrackerViewModel>();
        var window = new Windows.NotesTrackerWindow(viewModel);
        window.Show();
    }

}

