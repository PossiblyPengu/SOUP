using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BusinessToolsSuite.Desktop.Services;
using BusinessToolsSuite.Shared.Services;

namespace BusinessToolsSuite.Desktop.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly DialogService _dialogService;
    private readonly LauncherViewModel _launcherViewModel;
    private readonly NavigationService _navigationService;

    public IAsyncRelayCommand PickFilesCommand { get; }
    public IRelayCommand SkipCommand { get; }

    public WelcomeViewModel(DialogService dialogService, LauncherViewModel launcherViewModel, NavigationService navigationService)
    {
        _dialogService = dialogService;
        _launcherViewModel = launcherViewModel;
        _navigationService = navigationService;

        PickFilesCommand = new AsyncRelayCommand(async () => await PickFilesAsync());
        SkipCommand = new RelayCommand(() => Skip());
    }

    private async Task PickFilesAsync()
    {
        try
        {
            // First navigate to Allocation Buddy so the welcome overlay is hidden
            await _launcherViewModel.LaunchAllocationBuddyWithFiles(new string[0]);
        }
        catch { }

        var files = await _dialogService.ShowOpenFileDialogAsync("Select allocation files", "All Files", "xlsx", "csv");
        if (files == null || files.Length == 0) return;

        try
        {
            // Call the launcher helper which navigates (again) and triggers import on the AllocationBuddy VM
            await _launcherViewModel.LaunchAllocationBuddyWithFiles(files);
        }
        catch { }
    }

    private void Skip()
    {
        // Just navigate to the launcher (this will hide the welcome view)
        _navigationService.NavigateToLauncher();
    }
}
