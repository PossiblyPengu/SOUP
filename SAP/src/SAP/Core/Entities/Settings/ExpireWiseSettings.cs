using SAP.Core.Common;

namespace SAP.Core.Entities.Settings;

/// <summary>
/// Settings for ExpireWise application
/// </summary>
public class ExpireWiseSettings : BaseEntity
{
    public int WarningThresholdDays { get; set; } = 30;
    public int CriticalThresholdDays { get; set; } = 7;
    public string DefaultImportPath { get; set; } = string.Empty;
    public string DefaultExportPath { get; set; } = string.Empty;
    public int AutoRefreshIntervalMinutes { get; set; } = 0;
    public string Theme { get; set; } = "System";
    public bool ShowExpirationNotifications { get; set; } = true;
    public bool AutoLoadLastSession { get; set; } = true;
    
    /// <summary>
    /// Default status filter when opening: "All", "Good", "Warning", "Critical", "Expired"
    /// </summary>
    public string DefaultStatusFilter { get; set; } = "All";
    
    /// <summary>
    /// Date format for displaying expiration dates: "Short" (MM/dd/yyyy) or "Long" (MMMM dd, yyyy)
    /// </summary>
    public string DateDisplayFormat { get; set; } = "Short";
}
