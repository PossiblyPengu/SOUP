namespace SOUP.Core.Entities.Settings;

/// <summary>
/// Global application settings (not module-specific)
/// </summary>
public class ApplicationSettings
{
    // ===== Appearance =====
    
    /// <summary>
    /// When true, use dark theme; otherwise use light theme
    /// </summary>
    public bool IsDarkMode { get; set; } = true;

    /// <summary>
    /// When true, use basic Windows theme with no custom styling
    /// </summary>
    public bool UseBasicTheme { get; set; } = false;

    // ===== Startup =====
    
    /// <summary>
    /// The module to show when the application starts (e.g., "allocation", "essentials", "expirewise")
    /// Empty string means show the launcher/home
    /// </summary>
    public string DefaultModule { get; set; } = string.Empty;

    /// <summary>
    /// When true, automatically start the application when Windows starts
    /// </summary>
    public bool RunAtStartup { get; set; }

    /// <summary>
    /// When true, start the application minimized to tray
    /// </summary>
    public bool StartMinimizedToTray { get; set; }

    // ===== Widget Behavior =====
    
    /// <summary>
    /// When true, launch the Order Log widget as a separate window when the application starts
    /// </summary>
    public bool LaunchWidgetOnStartup { get; set; }

    /// <summary>
    /// When true, start with just the widget - main window won't open until explicitly requested
    /// </summary>
    public bool WidgetOnlyMode { get; set; }

    /// <summary>
    /// When true, widget continues running independently when the main window closes
    /// </summary>
    public bool KeepWidgetRunning { get; set; } = true;

    // ===== System Tray =====
    
    /// <summary>
    /// When true, show the app in system tray
    /// </summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>
    /// When true, closing the main window minimizes to system tray instead of closing
    /// </summary>
    public bool CloseToTray { get; set; }

    // ===== Behavior =====
    
    /// <summary>
    /// When true, show a confirmation dialog before exiting the application
    /// </summary>
    public bool ConfirmBeforeExit { get; set; }

    /// <summary>
    /// When true, remember and restore window positions on startup
    /// </summary>
    public bool RememberWindowPositions { get; set; } = true;
}
