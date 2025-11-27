namespace BusinessToolsSuite.Core.Entities.Settings;

/// <summary>
/// Settings for ExpireWise application
/// </summary>
public class ExpireWiseSettings
{
    /// <summary>
    /// Number of days before expiration to show warning status
    /// </summary>
    public int WarningThresholdDays { get; set; } = 30;

    /// <summary>
    /// Number of days before expiration to show critical status
    /// </summary>
    public int CriticalThresholdDays { get; set; } = 7;

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
    /// Whether to show notifications for items about to expire
    /// </summary>
    public bool ShowExpirationNotifications { get; set; } = true;

    /// <summary>
    /// Whether to automatically load last session on startup
    /// </summary>
    public bool AutoLoadLastSession { get; set; } = true;
}
