using CommunityToolkit.Mvvm.ComponentModel;
using BusinessToolsSuite.WPF.Services;

namespace BusinessToolsSuite.WPF.ViewModels;

/// <summary>
/// Main window ViewModel - Launcher with module navigation
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Business Tools Suite";

    public LauncherViewModel LauncherViewModel { get; }
    public NavigationService NavigationService { get; }

    public MainWindowViewModel(
        LauncherViewModel launcherViewModel,
        NavigationService navigationService)
    {
        LauncherViewModel = launcherViewModel;
        NavigationService = navigationService;

        // Update title when navigating to modules
        NavigationService.ModuleChanged += (_, moduleName) =>
        {
            Title = moduleName == "Launcher"
                ? "Business Tools Suite"
                : $"Business Tools Suite - {moduleName}";
        };
    }
}
