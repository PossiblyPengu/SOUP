using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace BusinessToolsSuite.Desktop.ViewModels;

// No longer used - launcher app doesn't need welcome screen
public partial class WelcomeViewModel : ViewModelBase
{
    public ICommand PickFilesCommand { get; } = new RelayCommand(() => { });
    public ICommand SkipCommand { get; } = new RelayCommand(() => { });
}
