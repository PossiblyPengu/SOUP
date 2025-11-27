using System;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using BusinessToolsSuite.WPF.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessToolsSuite.WPF.ViewModels;

/// <summary>
/// Launcher ViewModel for navigating to different modules
/// </summary>
public partial class LauncherViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly NavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LauncherViewModel>? _logger;

    [ObservableProperty]
    private bool _isDarkMode;

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

        // Initialize dark mode state
        _isDarkMode = _themeService.IsDarkMode;

        // Subscribe to theme changes
        _themeService.ThemeChanged += (_, isDark) =>
        {
            IsDarkMode = isDark;
        };
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
}
