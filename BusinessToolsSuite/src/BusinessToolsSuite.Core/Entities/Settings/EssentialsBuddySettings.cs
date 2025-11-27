namespace BusinessToolsSuite.Core.Entities.Settings;

/// <summary>
/// Settings for EssentialsBuddy application
/// </summary>
public class EssentialsBuddySettings
{
    /// <summary>
    /// Stock level below this threshold is considered low
    /// </summary>
    public int LowStockThreshold { get; set; } = 10;

    /// <summary>
    /// Stock level of 0 or below is considered out of stock
    /// </summary>
    public int OutOfStockThreshold { get; set; } = 0;

    /// <summary>
    /// Stock level above this threshold is considered overstocked
    /// </summary>
    public int OverstockThreshold { get; set; } = 100;

    /// <summary>
    /// Default folder path for importing files
    /// </summary>
    public string DefaultImportPath { get; set; } = string.Empty;

    /// <summary>
    /// Default folder path for exporting files
    /// </summary>
    public string DefaultExportPath { get; set; } = string.Empty;

    /// <summary>
    /// Auto-refresh interval in minutes (0 = disabled)
    /// </summary>
    public int AutoRefreshIntervalMinutes { get; set; } = 0;

    /// <summary>
    /// Theme preference (Light, Dark, System)
    /// </summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Whether to show notifications for low stock items
    /// </summary>
    public bool ShowLowStockNotifications { get; set; } = true;

    /// <summary>
    /// Whether to automatically load last session on startup
    /// </summary>
    public bool AutoLoadLastSession { get; set; } = true;
}
