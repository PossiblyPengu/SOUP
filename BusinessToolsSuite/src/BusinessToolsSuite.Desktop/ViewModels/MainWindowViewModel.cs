using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BusinessToolsSuite.Desktop.Services;

namespace BusinessToolsSuite.Desktop.ViewModels;

/// <summary>
/// Main window ViewModel
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;

    [ObservableProperty]
    private ViewModelBase _currentViewModel;

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
