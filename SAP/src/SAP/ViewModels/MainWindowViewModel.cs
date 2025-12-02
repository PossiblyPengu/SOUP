using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAP.Services;
using SAP.Views;

namespace SAP.ViewModels;

/// <summary>
/// Main window ViewModel - Launcher with module navigation
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindowViewModel>? _logger;

    [ObservableProperty]
    private string _title = "S.A.P";

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
                ? "S.A.P"
                : $"S.A.P - {moduleName}";
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
