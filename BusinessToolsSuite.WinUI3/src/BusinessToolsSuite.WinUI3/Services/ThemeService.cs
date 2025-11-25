using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Text.Json;

namespace BusinessToolsSuite.WinUI3.Services;

/// <summary>
/// Service for managing application theme (light/dark mode) in WinUI 3
/// </summary>
public partial class ThemeService : ObservableObject
{
    private readonly string _settingsPath;
    private const string SettingsFileName = "theme-settings.json";

    [ObservableProperty]
    private ElementTheme _currentTheme = ElementTheme.Light;

    public event EventHandler<ElementTheme>? ThemeChanged;

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
        CurrentTheme = CurrentTheme == ElementTheme.Light
            ? ElementTheme.Dark
            : ElementTheme.Light;

        ApplyTheme();
        SaveTheme();
    }

    /// <summary>
    /// Sets the theme explicitly
    /// </summary>
    public void SetTheme(ElementTheme theme)
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
        var window = (Application.Current as App)?.MainWindow;
        if (window?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = CurrentTheme;
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
                    CurrentTheme = settings.IsDarkMode ? ElementTheme.Dark : ElementTheme.Light;
                    ApplyTheme();
                }
            }
        }
        catch
        {
            // If loading fails, use default light theme
            CurrentTheme = ElementTheme.Light;
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
                IsDarkMode = CurrentTheme == ElementTheme.Dark
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
