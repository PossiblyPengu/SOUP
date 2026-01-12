namespace SOUP.Core;

/// <summary>
/// Application version information and changelog.
/// </summary>
/// <remarks>
/// <para>
/// This static class contains the application's version information,
/// release date, and changelog history. Update this class when
/// releasing new versions.
/// </para>
/// <para>
/// Version Format: MAJOR.MINOR.PATCH
/// - MAJOR: Breaking changes or major new features
/// - MINOR: New features, backwards compatible
/// - PATCH: Bug fixes and minor improvements
/// </para>
/// </remarks>
public static class AppVersion
{
    /// <summary>
    /// The current application version string (e.g., "4.6.1").
    /// </summary>
    public const string Version = "4.17.6";

    /// <summary>
    /// The current version display string with 'v' prefix (e.g., "v4.6.1").
    /// </summary>
    public const string DisplayVersion = "v" + Version;

    /// <summary>
    /// The release channel (Stable, Beta, Alpha, Dev).
    /// </summary>
    public const string Channel = "Stable";

    /// <summary>
    /// The build date in ISO format.
    /// </summary>
    public const string BuildDate = "2026-01-12";

    /// <summary>
    /// Full version string with channel (e.g., "v4.3.0 - Stable").
    /// </summary>
    public const string FullVersion = DisplayVersion + " - " + Channel;

    /// <summary>
    /// Gets the changelog entries for all versions.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Changelog { get; } = new List<ChangelogEntry>
    {
        new("4.17.6", "2026-01-12", "Release Update", new[]
        {
            "Enhance process termination logic during updates by killing all SOUP processes and logging errors"
        }),
        new("4.17.5", "2026-01-12", "Release Update", new[]
        {
            "Dispose tray icon service when hiding widget and before application shutdown"
        }),
        new("4.17.4", "2026-01-12", "Release Update", new[]
        {
            "Add Basic Windows theme and update theme application logic"
        }),
        new("4.17.3", "2026-01-12", "Release Update", new[]
        {
            "Refactor application shutdown process in AboutWindow and OrderLogWidgetWindow"
        }),
        new("4.17.2", "2026-01-12", "Release Update", new[]
        {
            "Implement update handling by bypassing closing confirmations during application updates"
        }),
        new("4.17.1", "2026-01-12", "Release Update", new[]
        {
            "Replace xcopy with robocopy for more reliable file copying during updates"
        }),
        new("4.17.0", "2026-01-12", "Release Update", new[]
        {
            "Update prerequisites to .NET 9 and add basic theme option in settings"
        }),
        new("4.16.0", "2026-01-10", "Release Update", new[]
        {
            "Add command to copy item redistribution data to clipboard"
        }),
        new("4.15.1", "2026-01-09", "Release Update", new[]
        {
            "Update button tooltip text for clarity in AllocationBuddyRPGView"
        }),
        new("4.15.0", "2026-01-09", "Release Update", new[]
        {
            "Update .NET SDK version in scripts and enhance AllocationBuddyRPGView with item view mode"
        }),
        new("4.14.2", "2026-01-09", "Release Update", new[]
        {
            "Enhance status message handling and update UI colors in OrderLog"
        }),
        new("4.14.1", "2026-01-09", "Release Update", new[]
        {
            "Enhance status message handling and update UI colors in OrderLog"
        }),
        new("4.14.0", "2026-01-09", "Release Update", new[]
        {
            "Update .NET SDK version and enhance Order Log widget functionality"
        }),
        new("4.13.0", "2026-01-09", "Release Update", new[]
        {
            "Refactor database integration from LiteDB to SQLite- Updated AllocationBuddyParser to reflect SQLite usage in comments.- Replaced LiteDB package reference with Microsoft.Data.Sqlite in project file.- Modified DictionarySyncService to batch upsert operations for items and stores.- Changed DictionaryManagementViewModel to log and manage data using SQLite.- Updated ExpirationItemDialogViewModel to retrieve stores from SQLite.- Implemented SqliteDbContext for managing SQLite connections and database initialization.- Created SqliteUnitOfWork for handling unit of work pattern with SQLite.- Developed SqliteRepository for CRUD operations with JSON serialization for entities.- Added migration tool to convert existing LiteDB databases to SQLite format."
        }),
        new("4.12.0", "2026-01-09", "Release Update", new[]
        {
            "Refactor export functionality to improve user experience and error handling",
            "refactored widget update pipeline"
        }),
        new("4.11.0", "2026-01-09", "Release Update", new[]
        {
            "Refactor widget handling and improve UI consistency with new message dialog"
        }),
        new("4.10.1", "2026-01-09", "Release Update", new[]
        {
            "Update to .NET 9.0 and enhance widget management- Changed target framework from net8.0 to net9.0 in project files.- Updated Microsoft.Extensions packages to version 9.0.0.- Introduced WidgetProcessService for managing the widget as a separate process, improving isolation and stability.- Updated release script to automatically add changelog entries.- Enhanced About window to display current version and changelog status.- Refactored various services to use the new WidgetProcessService."
        }),
        new("4.10.0", "2026-01-09", "Release Update", new[]
        {
            "Update to .NET 9.0 and enhance widget management"
        }),
        new("4.9.0", "2026-01-09", "Cleanup Release", new[]
        {
            "🎨 Added missing AccentBrush to theme files for consistency",
            "🧹 Removed FriendshipDungeonMG project",
            "🧹 Removed WPF DungeonCrawler easter egg",
            "🔧 Fixed About window changelog display",
            "📝 Release script now auto-updates changelog"
        }),
        new("4.6.13", "2026-01-08", "Release Update", new[]
        {
            "workflow fixes"
        }),
        new("4.6.12", "2026-01-08", "Release Update", new[]
        {
            "Adjusted defualt month in ExpireWise AddItem Dialog"
        }),
        new("4.6.11", "2026-01-08", "Release Update", new[]
        {
            "add item update"
        }),
        new("4.6.7", "2026-01-07", "Release Update", new[]
        {
            "Add PowerShell script for local HTTP server to serve SOUP updates"
        }),
        new("4.6.5", "2026-01-07", "Release Update", new[]
        {
            "feat: Update version to 4.6.5 and enhance release script with changelog prompts"
        }),
        new("4.6.4", "2026-01-07", "Release Infrastructure", new[]
        {
            "🚀 Added GitHub Actions release workflow",
            "🔄 Added automatic update checking via GitHub Releases",
            "🔒 Added security scanning for sensitive data",
            "📦 Enhanced release script with changelog prompts",
            "🖼️ Fixed tray icon not showing in published builds"
        }),



        new("4.2.0", "2025-12-05", "UI Polish Update", new[]
        {
            "🎨 Enhanced gradient backgrounds in both light and dark themes",
            "🌈 Sidebar now uses smooth gradient transitions",
            "🖼️ Module splash screens have more vibrant color gradients",
            "🪟 Title bar now matches sidebar color for seamless look",
            "📍 Moved About button into Settings window",
            "📜 Removed scroll from launcher sidebar navigation",
            "🔤 S.A.P title now follows theme colors",
            "📦 Installer now offers Full vs Portable installation options",
            "💾 Full install: Framework-dependent (~15 MB, requires .NET 8)",
            "📁 Portable install: Self-contained (~75 MB, runs anywhere)"
        }),

        new("4.1.0", "2025-12-05", "Quality of Life Update", new[]
        {
            "✨ Added About dialog with version and module information (F1)",
            "✨ Added window position and size persistence",
            "✨ Added keyboard shortcuts panel in launcher sidebar",
            "⌨️ Ctrl+T to toggle theme",
            "⌨️ Escape to return to launcher from any module",
            "⌨️ Alt+H alternative home shortcut",
            "📝 Added comprehensive XML documentation to all code",
            "🗂️ Reorganized project structure to modern standards",
            "🧹 Cleaned up temporary and backup files",
            "📋 Added README and improved .gitignore"
        }),

        new("4.0.0", "2025-12-01", "Fourth Major Release", new[]
        {
            "🚀 Fourth rendition of S.A.P (S.A.M. Add-on Pack)",
            "📦 ExpireWise - Expiration date tracking and management",
            "📊 AllocationBuddy - Store allocation management with RPG mode",
            "📋 EssentialsBuddy - Essential items inventory tracking",
            "🏷️ SwiftLabel - Quick label generation",
            "🌙 Dark and Light theme support with persistence",
            "💾 SQLite database for data persistence",
            "📤 Excel import/export support",
            "🗃️ Archive system for session management",
            "🔧 Modular installer with component selection"
        })
    };

    /// <summary>
    /// Gets the latest changelog entry.
    /// Returns the entry matching the current Version, or the first entry if no match.
    /// </summary>
    public static ChangelogEntry LatestChanges => 
        Changelog.FirstOrDefault(c => c.Version == Version) ?? Changelog[0];
}

/// <summary>
/// Represents a single version's changelog entry.
/// </summary>
/// <param name="Version">The version number (e.g., "1.1.0").</param>
/// <param name="Date">The release date in ISO format.</param>
/// <param name="Title">The release title/name.</param>
/// <param name="Changes">List of changes in this version.</param>
public record ChangelogEntry(
    string Version,
    string Date,
    string Title,
    IReadOnlyList<string> Changes
);
