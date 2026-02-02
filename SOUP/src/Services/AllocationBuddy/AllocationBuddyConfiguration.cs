using System.Text.Json;

namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Configuration settings for the AllocationBuddy module.
/// Centralizes all configurable values that were previously scattered as magic numbers.
/// </summary>
public class AllocationBuddyConfiguration
{
    /// <summary>
    /// Duration in milliseconds for the visual flash effect when items are updated.
    /// </summary>
    public int UpdateFlashDurationMs { get; set; } = 300;

    /// <summary>
    /// Maximum clipboard text length in bytes (10MB) to prevent denial-of-service attacks.
    /// </summary>
    public int MaxClipboardTextLengthBytes { get; set; } = 10_000_000;

    /// <summary>
    /// Default view mode when the module starts.
    /// </summary>
    public string DefaultViewMode { get; set; } = AllocationBuddyConstants.ViewModes.Stores;

    /// <summary>
    /// Default sort mode for item totals.
    /// </summary>
    public string DefaultSortMode { get; set; } = AllocationBuddyConstants.SortModes.QuantityDescending;

    /// <summary>
    /// Default clipboard format for copy operations.
    /// </summary>
    public string DefaultClipboardFormat { get; set; } = AllocationBuddyConstants.ClipboardFormats.TabSeparated;

    /// <summary>
    /// Whether to show confirmation dialogs for destructive operations.
    /// </summary>
    public bool ShowConfirmationDialogs { get; set; } = true;

    /// <summary>
    /// Whether to include item descriptions when copying to clipboard.
    /// </summary>
    public bool IncludeDescriptionsInCopy { get; set; } = false;

    /// <summary>
    /// JSON serialization options for archive operations.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Maximum number of session archives to keep (0 = unlimited).
    /// </summary>
    public int MaxSessionArchives { get; set; } = 50;

    /// <summary>
    /// Whether to auto-archive on shutdown.
    /// </summary>
    public bool AutoArchiveOnShutdown { get; set; } = true;

    /// <summary>
    /// Whether to restore the most recent archive on startup.
    /// </summary>
    public bool RestoreMostRecentOnStartup { get; set; } = true;
}
