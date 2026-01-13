using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SOUP.Services;

/// <summary>
/// Service for persisting and restoring window position, size, and state.
/// </summary>
/// <remarks>
/// <para>
/// This service saves window settings (position, size, state) to the user's AppData folder
/// and restores them on subsequent application launches.
/// </para>
/// <para>
/// Settings are stored per-window, identified by a unique window key.
/// The service handles multi-monitor scenarios and ensures windows open on visible screens.
/// </para>
/// </remarks>
public class WindowSettingsService
{
    private static readonly Lazy<WindowSettingsService> _instance = new(() => new WindowSettingsService(), isThreadSafe: true);

    /// <summary>
    /// Gets the singleton instance of the window settings service.
    /// </summary>
    public static WindowSettingsService Instance => _instance.Value;

    private readonly string _settingsDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowSettingsService"/> class.
    /// </summary>
    private WindowSettingsService()
    {
        _settingsDir = Path.Combine(Core.AppPaths.AppData, "WindowSettings");
        Directory.CreateDirectory(_settingsDir);
    }

    /// <summary>
    /// Saves the current window settings to disk.
    /// </summary>
    /// <param name="window">The window to save settings for.</param>
    /// <param name="windowKey">A unique key identifying this window type.</param>
    public void SaveWindowSettings(Window window, string windowKey)
    {
        try
        {
            var settings = new WindowSettings
            {
                Left = window.RestoreBounds.Left,
                Top = window.RestoreBounds.Top,
                Width = window.RestoreBounds.Width,
                Height = window.RestoreBounds.Height,
                IsMaximized = window.WindowState == WindowState.Maximized
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var filePath = GetSettingsPath(windowKey);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to save window settings for {WindowKey}", windowKey);
        }
    }

    /// <summary>
    /// Restores window settings from disk.
    /// </summary>
    /// <param name="window">The window to restore settings for.</param>
    /// <param name="windowKey">A unique key identifying this window type.</param>
    /// <returns><c>true</c> if settings were restored; <c>false</c> otherwise.</returns>
    public bool RestoreWindowSettings(Window window, string windowKey)
    {
        try
        {
            var filePath = GetSettingsPath(windowKey);
            if (!File.Exists(filePath))
                return false;

            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<WindowSettings>(json);

            if (settings == null)
                return false;

            // Validate that the position is on a visible screen
            if (IsPositionVisible(settings.Left, settings.Top, settings.Width, settings.Height))
            {
                window.Left = settings.Left;
                window.Top = settings.Top;
                window.Width = settings.Width;
                window.Height = settings.Height;
                window.WindowStartupLocation = WindowStartupLocation.Manual;

                if (settings.IsMaximized)
                {
                    window.WindowState = WindowState.Maximized;
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to restore window settings for {WindowKey}", windowKey);
        }

        return false;
    }

    /// <summary>
    /// Attaches save handlers to a window for automatic settings persistence.
    /// </summary>
    /// <param name="window">The window to attach handlers to.</param>
    /// <param name="windowKey">A unique key identifying this window type.</param>
    public void AttachToWindow(Window window, string windowKey)
    {
        // Try to restore settings on load
        window.SourceInitialized += (s, e) =>
        {
            RestoreWindowSettings(window, windowKey);
        };

        // Save settings on close
        window.Closing += (s, e) =>
        {
            SaveWindowSettings(window, windowKey);
        };
    }

    /// <summary>
    /// Gets the file path for the window settings file.
    /// </summary>
    private string GetSettingsPath(string windowKey)
    {
        var safeKey = string.Join("_", windowKey.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_settingsDir, $"{safeKey}.json");
    }

    /// <summary>
    /// Checks if a window position is visible on any connected monitor.
    /// Ensures that a significant portion (>50%) of the window is on a visible screen.
    /// </summary>
    private static bool IsPositionVisible(double left, double top, double width, double height)
    {
        var windowRect = new System.Drawing.Rectangle(
            (int)left, (int)top, (int)width, (int)height);

        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var intersection = System.Drawing.Rectangle.Intersect(
                windowRect, screen.WorkingArea);

            // Ensure significant portion (>50%) of window is visible
            var windowArea = windowRect.Width * windowRect.Height;
            var visibleArea = intersection.Width * intersection.Height;

            if (visibleArea > windowArea * 0.5)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Internal class for serializing window settings.
    /// </summary>
    private class WindowSettings
    {
        /// <summary>Gets or sets the left position.</summary>
        public double Left { get; set; }

        /// <summary>Gets or sets the top position.</summary>
        public double Top { get; set; }

        /// <summary>Gets or sets the width.</summary>
        public double Width { get; set; }

        /// <summary>Gets or sets the height.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets whether the window was maximized.</summary>
        public bool IsMaximized { get; set; }
    }
}
