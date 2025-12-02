using SAP.Core.Common;

namespace SAP.Core.Entities.Settings;

/// <summary>
/// Settings for EssentialsBuddy application
/// </summary>
public class EssentialsBuddySettings : BaseEntity
{
    public int LowStockThreshold { get; set; } = 10;
    public int OutOfStockThreshold { get; set; } = 0;
    public int OverstockThreshold { get; set; } = 100;
    public string DefaultImportPath { get; set; } = string.Empty;
    public string DefaultExportPath { get; set; } = string.Empty;
    public int AutoRefreshIntervalMinutes { get; set; } = 0;
    public string Theme { get; set; } = "System";
    public bool ShowLowStockNotifications { get; set; } = true;
    public bool AutoLoadLastSession { get; set; } = true;
    
    /// <summary>
    /// Default status filter when opening: "All", "Normal", "Low", "OutOfStock", "Overstocked"
    /// </summary>
    public string DefaultStatusFilter { get; set; } = "All";
    
    /// <summary>
    /// Start with "Essentials Only" filter enabled
    /// </summary>
    public bool DefaultEssentialsOnly { get; set; } = false;
}
