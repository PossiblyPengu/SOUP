using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Styling;
using BusinessToolsSuite.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace BusinessToolsSuite.Desktop.ViewModels;

/// <summary>
/// Launcher ViewModel for launching standalone applications
/// </summary>
public partial class LauncherViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly ILogger<LauncherViewModel>? _logger;

    [ObservableProperty]
    private bool _isDarkMode;

    public LauncherViewModel(
        ThemeService themeService,
        ILogger<LauncherViewModel>? logger = null)
    {
        _themeService = themeService;
        _logger = logger;

        // Initialize dark mode state
        _isDarkMode = _themeService.CurrentTheme == ThemeVariant.Dark;

        // Subscribe to theme changes
        _themeService.ThemeChanged += (_, theme) =>
        {
            IsDarkMode = theme == ThemeVariant.Dark;
        };
    }

    [RelayCommand]
    private void LaunchExpireWise()
    {
        _logger?.LogInformation("Launching ExpireWise standalone app");
        LaunchStandaloneApp("ExpireWise", "ExpireWiseApp");
    }

    [RelayCommand]
    private void LaunchAllocationBuddy()
    {
        _logger?.LogInformation("Launching AllocationBuddy standalone app");
        LaunchStandaloneApp("AllocationBuddy", "AllocationBuddyApp");
    }

    [RelayCommand]
    private void LaunchEssentialsBuddy()
    {
        _logger?.LogInformation("Launching EssentialsBuddy standalone app");
        LaunchStandaloneApp("EssentialsBuddy", "EssentialsBuddyApp");
    }

    private async void LaunchStandaloneApp(string appName, string directoryName)
    {
        try
        {
            // Get the current executable's directory
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;

            // Navigate up to find the standalone app
            // From: BusinessToolsSuite/src/BusinessToolsSuite.Desktop/bin/Debug/net8.0
            // To: {directoryName}/src/BusinessToolsSuite.Desktop/bin/Debug/net8.0/BusinessToolsSuite.Desktop.exe
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "..", ".."));
            var appPath = Path.Combine(baseDir, directoryName, "src", "BusinessToolsSuite.Desktop", "bin", "Debug", "net8.0", "BusinessToolsSuite.Desktop.exe");

            _logger?.LogInformation("Looking for {AppName} at: {AppPath}", appName, appPath);

            if (!File.Exists(appPath))
            {
                var errorMsg = $"Cannot find {appName} executable.\n\nExpected location:\n{appPath}\n\nPlease build the standalone app first using:\ndotnet build {directoryName}/{directoryName}.sln";
                _logger?.LogError(errorMsg);
                await ShowErrorDialog(appName, errorMsg);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(appPath)
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _logger?.LogInformation("Successfully launched {AppName} from {AppPath}", appName, appPath);
            }
            else
            {
                var errorMsg = $"Failed to start {appName}.\nThe process did not start.";
                _logger?.LogError(errorMsg);
                await ShowErrorDialog(appName, errorMsg);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error launching {appName}:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}";
            _logger?.LogError(ex, "Failed to launch {AppName} standalone app", appName);
            await ShowErrorDialog(appName, errorMsg);
        }
    }

    private async Task ShowErrorDialog(string appName, string message)
    {
        // Write error to console and log
        Console.WriteLine($"\n========== ERROR LAUNCHING {appName.ToUpper()} ==========");
        Console.WriteLine(message);
        Console.WriteLine("============================================\n");

        _logger?.LogError("Error launching {AppName}: {Message}", appName, message);

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _logger?.LogInformation("Toggling theme. Current: {Theme}", _themeService.CurrentTheme);
        _themeService.ToggleTheme();
    }
}
