using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SOUP.Core;
using SOUP.Core.Entities.AllocationBuddy;

namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Service responsible for persisting and archiving allocation data.
/// Handles saving, loading, and managing archive files.
/// </summary>
public class AllocationPersistenceService
{
    private readonly ILogger<AllocationPersistenceService>? _logger;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public AllocationPersistenceService(ILogger<AllocationPersistenceService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the archive directory path.
    /// </summary>
    /// <returns>The archive directory path.</returns>
    public static string GetArchivePath()
    {
        return Path.Combine(AppPaths.AllocationBuddyDir, AllocationBuddyConstants.Archive.DirectoryName);
    }

    /// <summary>
    /// Saves the current allocation data to an archive file.
    /// </summary>
    /// <param name="name">The name of the archive.</param>
    /// <param name="notes">Optional notes about the archive.</param>
    /// <param name="locations">The location allocations to save.</param>
    /// <param name="totalItems">The total number of items.</param>
    /// <param name="locationCount">The number of locations.</param>
    /// <returns>The file path of the created archive.</returns>
    public async Task<string> SaveArchiveAsync(
        string name,
        string? notes,
        ObservableCollection<LocationAllocation> locations,
        int totalItems,
        int locationCount)
    {
        var archivePath = GetArchivePath();
        Directory.CreateDirectory(archivePath);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var safeFileName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{timestamp}_{safeFileName}{AllocationBuddyConstants.Archive.FileExtension}";
        var filePath = Path.Combine(archivePath, fileName);

        var archiveData = CreateArchiveData(name, notes, locations, totalItems, locationCount);

        var json = JsonSerializer.Serialize(archiveData, s_jsonOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

        _logger?.LogInformation("Archived allocation data: {Name} ({Count} items) to {FilePath}",
            name, totalItems, filePath);

        return filePath;
    }

    /// <summary>
    /// Saves the current allocation data with a prefix (used for auto-archive and session saves).
    /// </summary>
    /// <param name="prefix">The prefix for the file name (e.g., "Auto-Archive", "Session-Save").</param>
    /// <param name="notes">Optional notes about the save.</param>
    /// <param name="locations">The location allocations to save.</param>
    /// <param name="totalItems">The total number of items.</param>
    /// <param name="locationCount">The number of locations.</param>
    /// <returns>The file path of the created archive.</returns>
    public async Task<string> SaveWithPrefixAsync(
        string prefix,
        string notes,
        ObservableCollection<LocationAllocation> locations,
        int totalItems,
        int locationCount)
    {
        var archivePath = GetArchivePath();
        Directory.CreateDirectory(archivePath);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{timestamp}_{prefix}{AllocationBuddyConstants.Archive.FileExtension}";
        var filePath = Path.Combine(archivePath, fileName);

        var name = $"{prefix} {DateTime.Now:MMM d, yyyy h:mm tt}";
        var archiveData = CreateArchiveData(name, notes, locations, totalItems, locationCount);

        var json = JsonSerializer.Serialize(archiveData, s_jsonOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

        _logger?.LogInformation("{Prefix} allocation data ({Count} items) to {FilePath}",
            prefix, totalItems, filePath);

        return filePath;
    }

    /// <summary>
    /// Creates archive data from location allocations.
    /// </summary>
    private ArchiveData CreateArchiveData(
        string name,
        string? notes,
        ObservableCollection<LocationAllocation> locations,
        int totalItems,
        int locationCount)
    {
        return new ArchiveData
        {
            Name = name,
            Notes = notes,
            ArchivedAt = DateTime.Now,
            TotalItems = totalItems,
            LocationCount = locationCount,
            Locations = locations.Select(loc => new ArchivedLocation
            {
                Location = loc.Location,
                LocationName = loc.LocationName,
                Items = loc.Items.Select(item => new ArchivedItem
                {
                    ItemNumber = item.ItemNumber,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    SKU = item.SKU
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Loads archive data from a file.
    /// </summary>
    /// <param name="filePath">The path to the archive file.</param>
    /// <returns>The archive data, or null if loading failed.</returns>
    public async Task<ArchiveData?> LoadArchiveAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<ArchiveData>(json);

            if (data != null)
            {
                _logger?.LogInformation("Loaded archive from {FilePath}: {Name}", filePath, data.Name);
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load archive from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Populates location allocations from archive data.
    /// </summary>
    /// <param name="archiveData">The archive data to load.</param>
    /// <param name="locations">The collection to populate with locations.</param>
    /// <param name="itemPool">The item pool collection.</param>
    public void PopulateFromArchive(
        ArchiveData archiveData,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        locations.Clear();
        itemPool.Clear();

        foreach (var archivedLoc in archiveData.Locations)
        {
            var location = new LocationAllocation
            {
                Location = archivedLoc.Location,
                LocationName = archivedLoc.LocationName
            };

            foreach (var archivedItem in archivedLoc.Items)
            {
                location.Items.Add(new ItemAllocation
                {
                    ItemNumber = archivedItem.ItemNumber,
                    Description = archivedItem.Description,
                    Quantity = archivedItem.Quantity,
                    SKU = archivedItem.SKU
                });
            }

            locations.Add(location);
        }
    }

    /// <summary>
    /// Gets all archive files ordered by creation time (newest first).
    /// </summary>
    /// <returns>List of archive file paths.</returns>
    public List<string> GetArchiveFiles()
    {
        var archivePath = GetArchivePath();

        if (!Directory.Exists(archivePath))
        {
            return new List<string>();
        }

        var files = Directory.GetFiles(archivePath, $"*{AllocationBuddyConstants.Archive.FileExtension}")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToList();

        return files;
    }

    /// <summary>
    /// Deletes an archive file.
    /// </summary>
    /// <param name="filePath">The path to the archive file to delete.</param>
    /// <returns>True if deletion succeeded.</returns>
    public bool DeleteArchive(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger?.LogInformation("Deleted archive: {FilePath}", filePath);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete archive: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Gets the most recent archive file (used for session restore).
    /// </summary>
    /// <returns>The path to the most recent archive, or null if none found.</returns>
    public string? GetMostRecentArchive()
    {
        var archivePath = GetArchivePath();

        if (!Directory.Exists(archivePath))
        {
            return null;
        }

        var mostRecent = Directory.GetFiles(archivePath, $"*{AllocationBuddyConstants.Archive.FileExtension}")
            .Select(f => new { Path = f, Info = new FileInfo(f) })
            .OrderByDescending(f => f.Info.LastWriteTime)
            .FirstOrDefault();

        return mostRecent?.Path;
    }

    /// <summary>
    /// Deletes session save archives (Session-Save and Session_* files).
    /// Used when clearing data to prevent auto-restore on next startup.
    /// </summary>
    /// <returns>Number of files deleted.</returns>
    public int DeleteSessionSaveArchives()
    {
        try
        {
            var archivePath = GetArchivePath();
            if (!Directory.Exists(archivePath))
            {
                return 0;
            }

            // Delete Session saves (Session-Save and Session_*) and Auto-Archive files
            var sessionFiles = Directory.GetFiles(archivePath, $"*_Session-Save{AllocationBuddyConstants.Archive.FileExtension}")
                .Concat(Directory.GetFiles(archivePath, $"*_{AllocationBuddyConstants.Archive.SessionPrefix}*{AllocationBuddyConstants.Archive.FileExtension}"))
                .Concat(Directory.GetFiles(archivePath, $"*_Auto-Archive{AllocationBuddyConstants.Archive.FileExtension}"))
                .ToList();

            int deletedCount = 0;
            foreach (var file in sessionFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger?.LogInformation("Deleted session archive: {FilePath}", file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete session archive: {FilePath}", file);
                }
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete session save archives");
            return 0;
        }
    }

    /// <summary>
    /// Extracts the original file name from a session archive path.
    /// </summary>
    /// <param name="archiveFilePath">The archive file path.</param>
    /// <returns>The extracted file name, or null if not a session archive.</returns>
    public string? ExtractSessionFileName(string archiveFilePath)
    {
        var archiveFileName = Path.GetFileNameWithoutExtension(archiveFilePath);

        if (archiveFileName.Contains("_Session_", StringComparison.Ordinal))
        {
            var sessionIndex = archiveFileName.IndexOf("_Session_", StringComparison.Ordinal);
            return archiveFileName[(sessionIndex + 9)..]; // Skip "_Session_"
        }

        return null;
    }
}
