using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Styling;
using BusinessToolsSuite.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessToolsSuite.Desktop.ViewModels;

/// <summary>
/// Launcher ViewModel for module selection
/// </summary>
public partial class LauncherViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;
    private readonly ThemeService _themeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LauncherViewModel>? _logger;

    [ObservableProperty]
    private bool _isDarkMode;

    public LauncherViewModel(
        NavigationService navigationService,
        ThemeService themeService,
        IServiceProvider serviceProvider,
        ILogger<LauncherViewModel>? logger = null)
    {
        _navigationService = navigationService;
        _themeService = themeService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Initialize dark mode state
        _isDarkMode = _themeService.CurrentTheme == ThemeVariant.Dark;

        // Subscribe to theme changes
        _themeService.ThemeChanged += (_, theme) =>
        {
            IsDarkMode = theme == ThemeVariant.Dark;
        };
    }

    [RelayCommand]
    private async Task LaunchExpireWise()
    {
        _logger?.LogInformation("Launching ExpireWise module");

        try
        {
            var expireWiseViewModel = _serviceProvider.GetRequiredService<Features.ExpireWise.ViewModels.ExpireWiseViewModel>();

            // Initialize the view model
            await expireWiseViewModel.InitializeAsync();

            // Create the view and set DataContext
            var expireWiseView = new Features.ExpireWise.Views.ExpireWiseView
            {
                DataContext = expireWiseViewModel
            };

            _navigationService.NavigateToModule("ExpireWise", expireWiseView);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to launch ExpireWise module");
        }
    }

    [RelayCommand]
    private async Task LaunchAllocationBuddy()
    {
        _logger?.LogInformation("Launching Allocation Buddy module");

        try
        {
            var allocationBuddyViewModel = _serviceProvider.GetRequiredService<Features.AllocationBuddy.ViewModels.AllocationBuddyViewModel>();

            // Initialize the view model
            await allocationBuddyViewModel.InitializeAsync();

            // Create the view and set DataContext
            var allocationBuddyView = new Features.AllocationBuddy.Views.AllocationBuddyView
            {
                DataContext = allocationBuddyViewModel
            };

            _navigationService.NavigateToModule("AllocationBuddy", allocationBuddyView);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to launch Allocation Buddy module");
        }
    }

    [RelayCommand]
    private void LaunchEssentialsBuddy()
    {
        _logger?.LogInformation("Launching Essentials Buddy module");
        // TODO: Create Essentials Buddy ViewModel and navigate
        // _navigationService.NavigateToModule("EssentialsBuddy", essentialsBuddyViewModel);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _logger?.LogInformation("Toggling theme. Current: {Theme}", _themeService.CurrentTheme);
        _themeService.ToggleTheme();
    }
}
