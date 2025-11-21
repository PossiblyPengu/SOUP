using CommunityToolkit.Mvvm.ComponentModel;
using BusinessToolsSuite.Desktop.Services;
using CommunityToolkit.Mvvm.Input;
using BusinessToolsSuite.Shared.Services;

namespace BusinessToolsSuite.Desktop.ViewModels;

/// <summary>
/// Main window ViewModel
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private string _title = "Business Tools Suite";

    [ObservableProperty]
    private bool _isLauncherVisible = true;

    public LauncherViewModel LauncherViewModel { get; }

    public MainWindowViewModel(NavigationService navigationService, LauncherViewModel launcherViewModel)
    {
        _navigationService = navigationService;
        LauncherViewModel = launcherViewModel;
        _currentViewModel = launcherViewModel;

        // Subscribe to navigation changes
        _navigationService.ModuleChanged += OnModuleChanged;
    }

    private void OnModuleChanged(object? sender, string moduleName)
    {
        IsLauncherVisible = moduleName == "Launcher";

        // Update the current view from navigation service
        if (moduleName != "Launcher")
        {
            CurrentViewModel = _navigationService.CurrentView;
        }
        else
        {
            CurrentViewModel = null;
        }

        Title = moduleName switch
        {
            "Launcher" => "Business Tools Suite",
            "ExpireWise" => "Business Tools Suite - ExpireWise",
            "AllocationBuddy" => "Business Tools Suite - Allocation Buddy",
            "EssentialsBuddy" => "Business Tools Suite - Essentials Buddy",
            _ => "Business Tools Suite"
        };
    }

    [RelayCommand]
    private void MinimizeWindow()
    {
        // Will be handled by code-behind
    }

    [RelayCommand]
    private void MaximizeWindow()
    {
        // Will be handled by code-behind
    }

    [RelayCommand]
    private void CloseWindow()
    {
        // Will be handled by code-behind
    }

    [RelayCommand]
    private void BackToLauncher()
    {
        _navigationService.NavigateToLauncher();
    }
}
