using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace SAP.Services;

/// <summary>
/// Service for managing application theme (light/dark mode) in WPF.
/// </summary>
/// <remarks>
/// <para>
/// This service provides centralized theme management for the application,
/// supporting dynamic switching between light and dark themes at runtime.
/// </para>
/// <para>
/// Theme preference is persisted to disk in the user's AppData folder and
/// automatically restored on application startup.
/// </para>
/// </remarks>
public partial class ThemeService : ObservableObject
{
    /// <summary>
    /// Gets the singleton instance of the theme service.
    /// </summary>
    private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService(), isThreadSafe: true);
    
    /// <summary>
    /// Gets the singleton instance of the theme service.
    /// </summary>
    public static ThemeService Instance => _instance.Value;

    private readonly string _settingsPath;
    private const string SettingsFileName = "theme-settings.json";

    /// <summary>
    /// Gets or sets whether dark mode is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isDarkMode = true;

    /// <summary>
    /// Gets or sets whether Windows 95 easter egg mode is active.
    /// </summary>
    [ObservableProperty]
    private bool _isWindows95Mode = false;

    /// <summary>
    /// Occurs when the theme changes between light and dark mode.
    /// </summary>
    public event EventHandler<bool>? ThemeChanged;

    /// <summary>
    /// Occurs when Windows 95 mode is toggled.
    /// </summary>
    public event EventHandler<bool>? Windows95ModeChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    public ThemeService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appDataPath, "SAP");
        Directory.CreateDirectory(settingsDir);
        _settingsPath = Path.Combine(settingsDir, SettingsFileName);

        LoadTheme();
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        ApplyTheme();
        SaveTheme();
    }

    /// <summary>
    /// Toggles Windows 95 easter egg mode.
    /// </summary>
    public void ToggleWindows95Mode()
    {
        IsWindows95Mode = !IsWindows95Mode;
        ApplyTheme();
        SaveTheme();
        Windows95ModeChanged?.Invoke(this, IsWindows95Mode);
    }

    /// <summary>
    /// Sets the theme explicitly to light or dark.
    /// </summary>
    /// <param name="isDark"><c>true</c> for dark theme; <c>false</c> for light theme.</param>
    public void SetTheme(bool isDark)
    {
        if (IsDarkMode != isDark)
        {
            IsDarkMode = isDark;
            ApplyTheme();
            SaveTheme();
        }
    }

    /// <summary>
    /// Initializes and applies the theme on application startup.
    /// </summary>
    public void Initialize()
    {
        ApplyTheme();
    }

    /// <summary>
    /// Applies the current theme by swapping ResourceDictionaries in the application.
    /// </summary>
    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        try
        {
            string themePath;
            if (IsWindows95Mode)
            {
                themePath = "pack://application:,,,/Themes/Windows98Theme.xaml";
            }
            else
            {
                themePath = IsDarkMode
                    ? "pack://application:,,,/Themes/DarkTheme.xaml"
                    : "pack://application:,,,/Themes/LightTheme.xaml";
            }

            var newTheme = new ResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Absolute)
            };

            // Find and remove existing theme dictionary
            var existingTheme = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme.xaml") == true);

            if (existingTheme != null)
            {
                app.Resources.MergedDictionaries.Remove(existingTheme);
            }

            // Add new theme dictionary
            app.Resources.MergedDictionaries.Add(newTheme);

            // Raise event for any listeners
            ThemeChanged?.Invoke(this, IsDarkMode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the theme preference from disk.
    /// </summary>
    private void LoadTheme()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json);

                if (settings != null)
                {
                    IsDarkMode = settings.IsDarkMode;
                    IsWindows95Mode = settings.IsWindows95Mode;
                    ApplyTheme();
                }
            }
        }
        catch (Exception ex)
        {
            // If loading fails, use default dark theme and log the error
            System.Diagnostics.Debug.WriteLine($"Failed to load theme settings: {ex.Message}");
            Serilog.Log.Warning(ex, "Failed to load theme settings, using default dark theme");
            IsDarkMode = true;
            IsWindows95Mode = false;
        }
    }

    /// <summary>
    /// Saves the theme preference to disk.
    /// </summary>
    private void SaveTheme()
    {
        try
        {
            var settings = new ThemeSettings
            {
                IsDarkMode = IsDarkMode,
                IsWindows95Mode = IsWindows95Mode
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the application
            System.Diagnostics.Debug.WriteLine($"Failed to save theme settings: {ex.Message}");
            Serilog.Log.Warning(ex, "Failed to save theme settings");
        }
    }

    /// <summary>
    /// Internal class for serializing theme settings to JSON.
    /// </summary>
    private class ThemeSettings
    {
        /// <summary>
        /// Gets or sets whether dark mode is enabled.
        /// </summary>
        public bool IsDarkMode { get; set; }

        /// <summary>
        /// Gets or sets whether Windows 95 easter egg mode is enabled.
        /// </summary>
        public bool IsWindows95Mode { get; set; }
    }
}
