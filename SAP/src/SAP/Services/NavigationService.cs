using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace SAP.Services;

/// <summary>
/// Service for managing navigation between application modules and views.
/// </summary>
/// <remarks>
/// <para>
/// This service provides centralized navigation management for the WPF application,
/// handling transitions between the main launcher and individual module views.
/// </para>
/// <para>
/// The service raises the <see cref="ModuleChanged"/> event when navigation occurs,
/// allowing UI elements to respond to view changes.
/// </para>
/// </remarks>
public partial class NavigationService : ObservableObject
{
    /// <summary>
    /// The current view content (ViewModel) being displayed.
    /// </summary>
    [ObservableProperty]
    private object? _currentView;

    /// <summary>
    /// The name of the currently active module.
    /// </summary>
    [ObservableProperty]
    private string _currentModuleName = "Launcher";

    /// <summary>
    /// Occurs when the active module changes.
    /// </summary>
    public event EventHandler<string>? ModuleChanged;

    /// <summary>
    /// Navigates back to the main launcher view.
    /// </summary>
    public void NavigateToLauncher()
    {
        CurrentModuleName = "Launcher";
        CurrentView = null;
        ModuleChanged?.Invoke(this, "Launcher");
    }

    /// <summary>
    /// Navigates to a specific module view.
    /// </summary>
    /// <param name="moduleName">The name of the module to navigate to.</param>
    /// <param name="viewModel">The ViewModel instance for the module.</param>
    public void NavigateToModule(string moduleName, object viewModel)
    {
        CurrentView = viewModel;
        CurrentModuleName = moduleName;
        ModuleChanged?.Invoke(this, moduleName);
    }

    /// <summary>
    /// Command to navigate back to the launcher.
    /// </summary>
    [RelayCommand]
    private void NavigateBack()
    {
        NavigateToLauncher();
    }
}
