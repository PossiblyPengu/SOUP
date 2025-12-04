using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace SAP.Services;

/// <summary>
/// Service for managing application theme (light/dark mode) in WPF
/// </summary>
public partial class ThemeService : ObservableObject
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private readonly string _settingsPath;
    private const string SettingsFileName = "theme-settings.json";

    [ObservableProperty]
    private bool _isDarkMode = true;

    public event EventHandler<bool>? ThemeChanged;

    public ThemeService()
    {
        _instance = this;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appDataPath, "SAP");
        Directory.CreateDirectory(settingsDir);
        _settingsPath = Path.Combine(settingsDir, SettingsFileName);

        LoadTheme();
    }

    /// <summary>
    /// Toggles between light and dark theme
    /// </summary>
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        ApplyTheme();
        SaveTheme();
    }

    /// <summary>
    /// Sets the theme explicitly
    /// </summary>
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
    /// Initialize and apply the theme on app startup
    /// </summary>
    public void Initialize()
    {
        ApplyTheme();
    }

    /// <summary>
    /// Applies the current theme to the application by swapping ResourceDictionaries
    /// </summary>
    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        try
        {
            var themePath = IsDarkMode
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";

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
    /// Loads theme preference from disk
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
        }
    }

    /// <summary>
    /// Saves theme preference to disk
    /// </summary>
    private void SaveTheme()
    {
        try
        {
            var settings = new ThemeSettings
            {
                IsDarkMode = IsDarkMode
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

    private class ThemeSettings
    {
        public bool IsDarkMode { get; set; }
    }
}
