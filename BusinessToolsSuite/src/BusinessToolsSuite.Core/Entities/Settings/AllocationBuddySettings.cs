namespace BusinessToolsSuite.Core.Entities.Settings;

/// <summary>
/// Settings for AllocationBuddy application
/// </summary>
public class AllocationBuddySettings
{
    /// <summary>
    /// Default folder path for importing files
    /// </summary>
    public string DefaultImportPath { get; set; } = string.Empty;

    /// <summary>
    /// Default folder path for exporting files
    /// </summary>
    public string DefaultExportPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to dictionary file for item lookups
    /// </summary>
    public string DictionaryFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Auto-save interval in minutes (0 = disabled)
    /// </summary>
    public int AutoSaveIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Whether to show confirmation dialogs for destructive actions
    /// </summary>
    public bool ShowConfirmationDialogs { get; set; } = true;

    /// <summary>
    /// Theme preference (Light, Dark, System)
    /// </summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Whether to automatically load last session on startup
    /// </summary>
    public bool AutoLoadLastSession { get; set; } = false;
}
