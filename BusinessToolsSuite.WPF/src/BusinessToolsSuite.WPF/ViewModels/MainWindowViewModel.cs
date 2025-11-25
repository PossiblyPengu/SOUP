using CommunityToolkit.Mvvm.ComponentModel;

namespace BusinessToolsSuite.WPF.ViewModels;

/// <summary>
/// Main window ViewModel - Launcher for standalone apps
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Business Tools Suite Launcher";

    public LauncherViewModel LauncherViewModel { get; }

    public MainWindowViewModel(LauncherViewModel launcherViewModel)
    {
        LauncherViewModel = launcherViewModel;
    }
}
