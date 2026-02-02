namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Constants for AllocationBuddy module to eliminate magic strings and values.
/// </summary>
public static class AllocationBuddyConstants
{
    /// <summary>
    /// View modes for displaying allocation data.
    /// </summary>
    public static class ViewModes
    {
        /// <summary>
        /// Display allocations organized by store/location.
        /// </summary>
        public const string Stores = "stores";

        /// <summary>
        /// Display allocations organized by item.
        /// </summary>
        public const string Items = "items";
    }

    /// <summary>
    /// Sort modes for item totals.
    /// </summary>
    public static class SortModes
    {
        /// <summary>
        /// Sort by quantity descending (highest first).
        /// </summary>
        public const string QuantityDescending = "qty-desc";

        /// <summary>
        /// Sort by quantity ascending (lowest first).
        /// </summary>
        public const string QuantityAscending = "qty-asc";

        /// <summary>
        /// Sort alphabetically by item number ascending.
        /// </summary>
        public const string ItemNumberAscending = "item-asc";

        /// <summary>
        /// Sort alphabetically by item number descending.
        /// </summary>
        public const string ItemNumberDescending = "item-desc";
    }

    /// <summary>
    /// Clipboard format options.
    /// </summary>
    public static class ClipboardFormats
    {
        /// <summary>
        /// Tab-separated values format.
        /// </summary>
        public const string TabSeparated = "TabSeparated";

        /// <summary>
        /// Comma-separated values format.
        /// </summary>
        public const string CommaSeparated = "CommaSeparated";

        /// <summary>
        /// Space-separated values format.
        /// </summary>
        public const string SpaceSeparated = "SpaceSeparated";
    }

    /// <summary>
    /// Settings keys for persisting configuration.
    /// </summary>
    public static class SettingsKeys
    {
        /// <summary>
        /// Settings key for AllocationBuddy module.
        /// </summary>
        public const string ModuleName = "AllocationBuddy";
    }

    /// <summary>
    /// Archive file patterns and extensions.
    /// </summary>
    public static class Archive
    {
        /// <summary>
        /// File extension for archive files.
        /// </summary>
        public const string FileExtension = ".json";

        /// <summary>
        /// Prefix for session archive files.
        /// </summary>
        public const string SessionPrefix = "Session_";

        /// <summary>
        /// Archive directory name.
        /// </summary>
        public const string DirectoryName = "Archives";
    }
}
