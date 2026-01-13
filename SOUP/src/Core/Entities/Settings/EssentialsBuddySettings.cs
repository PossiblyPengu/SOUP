using SOUP.Core.Common;

namespace SOUP.Core.Entities.Settings;

/// <summary>
/// Configuration settings for the EssentialsBuddy module.
/// </summary>
/// <remarks>
/// These settings control stock level thresholds, notifications,
/// and default filter states for the inventory tracking module.
/// </remarks>
public class EssentialsBuddySettings : BaseEntity
{
    /// <summary>
    /// Gets or sets the quantity threshold for low stock warnings.
    /// </summary>
    public int LowStockThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets the quantity threshold for out-of-stock status.
    /// </summary>
    public int OutOfStockThreshold { get; set; } = 0;

    /// <summary>
    /// Gets or sets the quantity threshold for sufficient stock status.
    /// </summary>
    public int SufficientThreshold { get; set; } = 100;

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
    /// Gets or sets whether to show low stock notifications.
    /// </summary>
    public bool ShowLowStockNotifications { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically load the last session on startup.
    /// </summary>
    public bool AutoLoadLastSession { get; set; } = true;

    /// <summary>
    /// Gets or sets the default status filter: "All", "Normal", "Low", "OutOfStock", or "Sufficient".
    /// </summary>
    public string DefaultStatusFilter { get; set; } = "All";

    /// <summary>
    /// Gets or sets whether to start with "Essentials Only" filter enabled.
    /// </summary>
    public bool DefaultEssentialsOnly { get; set; } = false;
}
