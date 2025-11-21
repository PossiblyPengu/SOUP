using System;
using BusinessToolsSuite.Desktop.Services;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusinessToolsSuite.Desktop.Services;

/// <summary>
/// Service for managing application theme (light/dark mode)
/// </summary>
public partial class ThemeService : ObservableObject
{
    private readonly string _settingsPath;
    private const string SettingsFileName = "theme-settings.json";

    [ObservableProperty]
    private ThemeVariant _currentTheme = ThemeVariant.Light;

    public event EventHandler<ThemeVariant>? ThemeChanged;

    public ThemeService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appDataPath, "BusinessToolsSuite");
        Directory.CreateDirectory(settingsDir);
        _settingsPath = Path.Combine(settingsDir, SettingsFileName);

        LoadTheme();
    }

    /// <summary>
    /// Toggles between light and dark theme
    /// </summary>
    public void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == ThemeVariant.Light
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        ApplyTheme();
        SaveTheme();
    }

    /// <summary>
    /// Sets the theme explicitly
    /// </summary>
    public void SetTheme(ThemeVariant theme)
    {
        if (CurrentTheme != theme)
        {
            CurrentTheme = theme;
            ApplyTheme();
            SaveTheme();
        }
    }

    /// <summary>
    /// Applies the current theme to the application
    /// </summary>
    private void ApplyTheme()
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = CurrentTheme;
            ThemeChanged?.Invoke(this, CurrentTheme);
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
                    CurrentTheme = settings.IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
                    ApplyTheme();
                }
            }
        }
        catch
        {
            // If loading fails, use default light theme
            CurrentTheme = ThemeVariant.Light;
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
                IsDarkMode = CurrentTheme == ThemeVariant.Dark
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
