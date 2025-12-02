using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SAP.Services;

/// <summary>
/// Service for managing application theme (light/dark mode) in WPF
/// </summary>
public partial class ThemeService : ObservableObject
{
    private readonly string _settingsPath;
    private const string SettingsFileName = "theme-settings.json";

    [ObservableProperty]
    private bool _isDarkMode;

    public event EventHandler<bool>? ThemeChanged;

    public ThemeService()
    {
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
    /// Applies the current theme to the application
    /// </summary>
    private void ApplyTheme()
    {
        // WPF theme application would go here
        // For now, just raise the event
        ThemeChanged?.Invoke(this, IsDarkMode);
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
        catch
        {
            // If loading fails, use default light theme
            IsDarkMode = false;
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
        catch
        {
            // Silently fail if saving doesn't work
        }
    }

    private class ThemeSettings
    {
        public bool IsDarkMode { get; set; }
    }
}
