using SAP.Core.Common;

namespace SAP.Core.Entities.Settings;

/// <summary>
/// Configuration settings for the ExpireWise module.
/// </summary>
/// <remarks>
/// These settings control expiration thresholds, date display formats,
/// and default filter states for the expiration tracking module.
/// </remarks>
public class ExpireWiseSettings : BaseEntity
{
    /// <summary>
    /// Gets or sets the number of days before expiration to show warning status.
    /// </summary>
    public int WarningThresholdDays { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets the number of days before expiration to show critical status.
    /// </summary>
    public int CriticalThresholdDays { get; set; } = 7;
    
    /// <summary>
    /// Gets or sets the default path for importing files.
    /// </summary>
    public string DefaultImportPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the default path for exporting files.
    /// </summary>
    public string DefaultExportPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the auto-refresh interval in minutes (0 = disabled).
    /// </summary>
    public int AutoRefreshIntervalMinutes { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the UI theme preference.
    /// </summary>
    public string Theme { get; set; } = "System";
    
    /// <summary>
    /// Gets or sets whether to show expiration notifications.
    /// </summary>
    public bool ShowExpirationNotifications { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to automatically load the last session on startup.
    /// </summary>
    public bool AutoLoadLastSession { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the default status filter: "All", "Good", "Warning", "Critical", or "Expired".
    /// </summary>
    public string DefaultStatusFilter { get; set; } = "All";
    
    /// <summary>
    /// Gets or sets the date display format: "Short" (MM/dd/yyyy) or "Long" (MMMM dd, yyyy).
    /// </summary>
    public string DateDisplayFormat { get; set; } = "Short";
}
