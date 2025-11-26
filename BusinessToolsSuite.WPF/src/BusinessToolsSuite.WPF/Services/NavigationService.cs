using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace BusinessToolsSuite.WPF.Services;

/// <summary>
/// Navigation service for managing view transitions in WPF
/// </summary>
public partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentModuleName = "Launcher";

    public event EventHandler<string>? ModuleChanged;

    public void NavigateToLauncher()
    {
        CurrentModuleName = "Launcher";
        CurrentView = null;
        ModuleChanged?.Invoke(this, "Launcher");
    }

    public void NavigateToModule(string moduleName, object viewModel)
    {
        CurrentView = viewModel;
        CurrentModuleName = moduleName;
        ModuleChanged?.Invoke(this, moduleName);
    }

    [RelayCommand]
    private void NavigateBack()
    {
        NavigateToLauncher();
    }
}
