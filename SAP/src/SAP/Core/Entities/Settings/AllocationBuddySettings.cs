using SAP.Core.Common;

namespace SAP.Core.Entities.Settings;

/// <summary>
/// Settings for AllocationBuddy application
/// </summary>
public class AllocationBuddySettings : BaseEntity
{
    public string DefaultImportPath { get; set; } = string.Empty;
    public string DefaultExportPath { get; set; } = string.Empty;
    public string DictionaryFilePath { get; set; } = string.Empty;
    public int AutoSaveIntervalMinutes { get; set; } = 5;
    public bool ShowConfirmationDialogs { get; set; } = true;
    public string Theme { get; set; } = "System";
    public bool AutoLoadLastSession { get; set; } = false;
    
    /// <summary>
    /// Include item descriptions when copying to clipboard
    /// </summary>
    public bool IncludeDescriptionsInCopy { get; set; } = false;
    
    /// <summary>
    /// Clipboard format: "TabSeparated" or "CommaSeparated"
    /// </summary>
    public string ClipboardFormat { get; set; } = "TabSeparated";
}
