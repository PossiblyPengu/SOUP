using System;
using BusinessToolsSuite.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusinessToolsSuite.Desktop.Services;

/// <summary>
/// Navigation service for managing view transitions
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
        ModuleChanged?.Invoke(this, "Launcher");
    }

    public void NavigateToModule(string moduleName, object viewModel)
    {
        CurrentView = viewModel;
        CurrentModuleName = moduleName;
        ModuleChanged?.Invoke(this, moduleName);
    }

    public void GoBack()
    {
        NavigateToLauncher();
    }
}
