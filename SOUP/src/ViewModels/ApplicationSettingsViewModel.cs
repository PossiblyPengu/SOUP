using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using SOUP.Core.Entities.Settings;
using SOUP.Infrastructure.Services;
using SOUP.Services;

namespace SOUP.ViewModels;

/// <summary>
/// ViewModel for global application settings
/// </summary>
public partial class ApplicationSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private bool _isLoading;

    // ===== Appearance =====

    [ObservableProperty]
    private bool _isDarkMode = true;

    // ===== Startup =====

    [ObservableProperty]
    private string _defaultModule = string.Empty;

    [ObservableProperty]
    private bool _runAtStartup;

    [ObservableProperty]
    private bool _startMinimizedToTray;

    // ===== Widget Behavior =====

    [ObservableProperty]
    private bool _launchWidgetOnStartup;

    [ObservableProperty]
    private bool _widgetOnlyMode;

    [ObservableProperty]
    private bool _keepWidgetRunning = true;

    /// <summary>
    /// Whether the OrderLog module is enabled
    /// </summary>
    public bool IsOrderLogEnabled => ModuleConfiguration.Instance.OrderLogEnabled;

    // ===== System Tray =====

    [ObservableProperty]
    private bool _showTrayIcon = true;

    [ObservableProperty]
    private bool _closeToTray;

    // ===== Behavior =====

    [ObservableProperty]
    private bool _confirmBeforeExit;

    [ObservableProperty]
    private bool _rememberWindowPositions = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Available modules for the default module dropdown
    /// </summary>
    public List<ModuleOption> AvailableModules { get; } = new();

    /// <summary>
    /// Event raised when settings change and need to be applied
    /// </summary>
    public event Action? SettingsApplied;

    public ApplicationSettingsViewModel(SettingsService settingsService, ThemeService themeService)
    {
        _settingsService = settingsService;
        _themeService = themeService;

        // Build available modules list based on enabled modules
        BuildAvailableModules();

        _ = LoadSettingsAsync();
    }

    private void BuildAvailableModules()
    {
        var config = ModuleConfiguration.Instance;

        AvailableModules.Add(new ModuleOption("", "Home (Launcher)"));

        if (config.AllocationBuddyEnabled)
            AvailableModules.Add(new ModuleOption("allocation", "AllocationBuddy"));
        if (config.EssentialsBuddyEnabled)
            AvailableModules.Add(new ModuleOption("essentials", "EssentialsBuddy"));
        if (config.ExpireWiseEnabled)
            AvailableModules.Add(new ModuleOption("expirewise", "ExpireWise"));
    }

    // ===== Property Change Handlers =====

    partial void OnIsDarkModeChanged(bool value)
    {
        if (!_isLoading)
        {
            _themeService.SetTheme(value);
            _ = SaveSettingsAsync();
        }
    }

    partial void OnDefaultModuleChanged(string value)
    {
        if (!_isLoading)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        if (!_isLoading)
        {
            SetRunAtStartup(value);
            _ = SaveSettingsAsync();
        }
    }

    partial void OnLaunchWidgetOnStartupChanged(bool value)
    {
        if (!_isLoading)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnWidgetOnlyModeChanged(bool value)
    {
        if (!_isLoading)
        {
            // If enabling widget-only mode, also enable launch widget on startup
            if (value && !LaunchWidgetOnStartup)
            {
                LaunchWidgetOnStartup = true;
            }
            _ = SaveSettingsAsync();
        }
    }

    partial void OnKeepWidgetRunningChanged(bool value)
    {
        if (!_isLoading)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (!_isLoading)
        {
            // If enabling close to tray, ensure tray icon is shown
            if (value && !ShowTrayIcon)
            {
                ShowTrayIcon = true;
            }
            _ = SaveSettingsAsync();
        }
    }

    partial void OnShowTrayIconChanged(bool value)
    {
        if (!_isLoading)
        {
            // If hiding tray icon, disable close to tray and start minimized
            if (!value)
            {
                if (CloseToTray) CloseToTray = false;
                if (StartMinimizedToTray) StartMinimizedToTray = false;
            }
            _ = SaveSettingsAsync();
        }
    }

    partial void OnStartMinimizedToTrayChanged(bool value)
    {
        if (!_isLoading)
        {
            // If enabling start minimized, ensure tray icon is shown
            if (value && !ShowTrayIcon)
            {
                ShowTrayIcon = true;
            }
            _ = SaveSettingsAsync();
        }
    }

    partial void OnConfirmBeforeExitChanged(bool value)
    {
        if (!_isLoading)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnRememberWindowPositionsChanged(bool value)
    {
        if (!_isLoading)
        {
            _ = SaveSettingsAsync();
        }
    }

    // ===== Load/Save =====

    private async Task LoadSettingsAsync()
    {
        try
        {
            _isLoading = true;
            var settings = await _settingsService.LoadSettingsAsync<ApplicationSettings>("Application");

            // Appearance
            IsDarkMode = settings.IsDarkMode;

            // Startup
            DefaultModule = settings.DefaultModule;
            RunAtStartup = settings.RunAtStartup;
            StartMinimizedToTray = settings.StartMinimizedToTray;

            // Widget Behavior
            LaunchWidgetOnStartup = settings.LaunchWidgetOnStartup;
            WidgetOnlyMode = settings.WidgetOnlyMode;
            KeepWidgetRunning = settings.KeepWidgetRunning;

            // System Tray
            ShowTrayIcon = settings.ShowTrayIcon;
            CloseToTray = settings.CloseToTray;

            // Behavior
            ConfirmBeforeExit = settings.ConfirmBeforeExit;
            RememberWindowPositions = settings.RememberWindowPositions;

            // Sync theme with ThemeService (in case it was changed elsewhere)
            if (_themeService.IsDarkMode != IsDarkMode)
            {
                _themeService.SetTheme(IsDarkMode);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load application settings");
            StatusMessage = "Failed to load settings";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new ApplicationSettings
            {
                // Appearance
                IsDarkMode = IsDarkMode,

                // Startup
                DefaultModule = DefaultModule,
                RunAtStartup = RunAtStartup,
                StartMinimizedToTray = StartMinimizedToTray,

                // Widget Behavior
                LaunchWidgetOnStartup = LaunchWidgetOnStartup,
                WidgetOnlyMode = WidgetOnlyMode,
                KeepWidgetRunning = KeepWidgetRunning,

                // System Tray
                ShowTrayIcon = ShowTrayIcon,
                CloseToTray = CloseToTray,

                // Behavior
                ConfirmBeforeExit = ConfirmBeforeExit,
                RememberWindowPositions = RememberWindowPositions
            };

            await _settingsService.SaveSettingsAsync("Application", settings);
            StatusMessage = "Settings saved";
            SettingsApplied?.Invoke();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to save application settings");
            StatusMessage = "Failed to save settings";
        }
    }

    // ===== Windows Startup Registry =====

    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SOUP";

    private void SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    Serilog.Log.Information("Added SOUP to Windows startup");
                }
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                Serilog.Log.Information("Removed SOUP from Windows startup");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to modify Windows startup registry");
            StatusMessage = "Failed to modify startup settings";
        }
    }
}

/// <summary>
/// Represents a module option for the default module dropdown
/// </summary>
public class ModuleOption
{
    public string Value { get; }
    public string DisplayName { get; }

    public ModuleOption(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}
