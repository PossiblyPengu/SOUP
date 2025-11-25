using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace BusinessToolsSuite.WinUI3.Services;

/// <summary>
/// Navigation service for managing view transitions in WinUI 3
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
