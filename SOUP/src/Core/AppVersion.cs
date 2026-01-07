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
    public const string Version = "4.6.8";

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
    public const string BuildDate = "2026-01-07";

    /// <summary>
    /// Full version string with channel (e.g., "v4.3.0 - Stable").
    /// </summary>
    public const string FullVersion = DisplayVersion + " - " + Channel;

    /// <summary>
    /// Gets the changelog entries for all versions.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Changelog { get; } = new List<ChangelogEntry>
    {
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

        new("4.6.1", "2026-01-06", "Maintenance Update", new[]
        {
            "🔧 Fixed hallucinated WPF project references in play-dungeon scripts",
            "🧹 Cleaned up non-existent FriendshipDungeon project references",
            "📝 Simplified dungeon launcher to MonoGame-only"
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
            "💾 LiteDB database for data persistence",
            "📤 Excel import/export support",
            "🗃️ Archive system for session management",
            "🔧 Modular installer with component selection"
        })
    };

    /// <summary>
    /// Gets the latest changelog entry.
    /// </summary>
    public static ChangelogEntry LatestChanges => Changelog[0];
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
