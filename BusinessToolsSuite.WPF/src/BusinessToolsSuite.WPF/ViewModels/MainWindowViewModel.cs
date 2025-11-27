using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BusinessToolsSuite.WPF.Services;
using BusinessToolsSuite.WPF.Views;

namespace BusinessToolsSuite.WPF.ViewModels;

/// <summary>
/// Main window ViewModel - Launcher with module navigation
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindowViewModel>? _logger;

    [ObservableProperty]
    private string _title = "Business Tools Suite";

    public LauncherViewModel LauncherViewModel { get; }
    public NavigationService NavigationService { get; }

    public MainWindowViewModel(
        LauncherViewModel launcherViewModel,
        NavigationService navigationService,
        IServiceProvider serviceProvider,
        ILogger<MainWindowViewModel>? logger = null)
    {
        LauncherViewModel = launcherViewModel;
        NavigationService = navigationService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Update title when navigating to modules
        NavigationService.ModuleChanged += (_, moduleName) =>
        {
            Title = moduleName == "Launcher"
                ? "Business Tools Suite"
                : $"Business Tools Suite - {moduleName}";
        };
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
}
