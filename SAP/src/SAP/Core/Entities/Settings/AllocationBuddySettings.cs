using SAP.Core.Common;

namespace SAP.Core.Entities.Settings;

/// <summary>
/// Configuration settings for the AllocationBuddy module.
/// </summary>
/// <remarks>
/// These settings are persisted to the user's AppData folder and
/// control various aspects of the allocation tracking behavior.
/// </remarks>
public class AllocationBuddySettings : BaseEntity
{
    /// <summary>
    /// Gets or sets the default path for importing files.
    /// </summary>
    public string DefaultImportPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the default path for exporting files.
    /// </summary>
    public string DefaultExportPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the path to the dictionary file.
    /// </summary>
    public string DictionaryFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the auto-save interval in minutes.
    /// </summary>
    public int AutoSaveIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets whether to show confirmation dialogs.
    /// </summary>
    public bool ShowConfirmationDialogs { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the UI theme preference.
    /// </summary>
    public string Theme { get; set; } = "System";
    
    /// <summary>
    /// Gets or sets whether to automatically load the last session on startup.
    /// </summary>
    public bool AutoLoadLastSession { get; set; } = false;
    
    /// <summary>
    /// Gets or sets whether to include item descriptions when copying to clipboard.
    /// </summary>
    public bool IncludeDescriptionsInCopy { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the clipboard format: "TabSeparated" or "CommaSeparated".
    /// </summary>
    public string ClipboardFormat { get; set; } = "TabSeparated";
}
