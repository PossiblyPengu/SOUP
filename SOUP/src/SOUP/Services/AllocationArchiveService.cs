using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SOUP.Services;

/// <summary>
/// Service for managing AllocationBuddy session archives.
/// Uses generic JSON storage to avoid coupling to domain types.
/// </summary>
public interface IAllocationArchiveService
{
    /// <summary>
    /// Gets the list of available archives.
    /// </summary>
    IReadOnlyList<ArchiveInfo> Archives { get; }
    
    /// <summary>
    /// Loads the list of available archives from disk.
    /// </summary>
    Task LoadArchivesAsync();
    
    /// <summary>
    /// Archives the current session data as JSON.
    /// </summary>
    /// <typeparam name="TLocation">Type of location allocation.</typeparam>
    /// <typeparam name="TItem">Type of item allocation.</typeparam>
    /// <param name="locationAllocations">Current location allocations.</param>
    /// <param name="itemPool">Current item pool.</param>
    /// <param name="sessionName">Optional session name.</param>
    /// <returns>The created archive info, or null if archiving failed.</returns>
    Task<ArchiveInfo?> CreateArchiveAsync<TLocation, TItem>(
        IEnumerable<TLocation> locationAllocations,
        IEnumerable<TItem> itemPool,
        string? sessionName = null);
    
    /// <summary>
    /// Loads data from an archive.
    /// </summary>
    /// <typeparam name="TLocation">Type of location allocation.</typeparam>
    /// <typeparam name="TItem">Type of item allocation.</typeparam>
    /// <param name="archive">The archive to load.</param>
    /// <returns>The archived data, or null if loading failed.</returns>
    Task<ArchivedSession<TLocation, TItem>?> LoadArchiveAsync<TLocation, TItem>(ArchiveInfo archive);
    
    /// <summary>
    /// Deletes an archive.
    /// </summary>
    /// <param name="archive">The archive to delete.</param>
    /// <returns>True if deleted successfully.</returns>
    Task<bool> DeleteArchiveAsync(ArchiveInfo archive);
    
    /// <summary>
    /// Gets the most recent archive.
    /// </summary>
    ArchiveInfo? GetMostRecentArchive();
}

/// <summary>
/// Information about an archive.
/// </summary>
public class ArchiveInfo
{
    public required string Id { get; init; }
    public required string FilePath { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string DisplayName { get; init; }
    public int LocationCount { get; init; }
    public int ItemCount { get; init; }
}

/// <summary>
/// Archived session data (generic).
/// </summary>
public class ArchivedSession<TLocation, TItem>
{
    public List<TLocation> LocationAllocations { get; init; } = new();
    public List<TItem> ItemPool { get; init; } = new();
    public DateTime ArchivedAt { get; init; }
    public string? SessionName { get; init; }
}

/// <summary>
/// Internal storage format for archives.
/// </summary>
internal class ArchiveMetadata
{
    public int LocationCount { get; set; }
    public int ItemCount { get; set; }
    public DateTime ArchivedAt { get; set; }
    public string? SessionName { get; set; }
}

/// <summary>
/// Implementation of IAllocationArchiveService using file system storage.
/// </summary>
public class AllocationArchiveService : IAllocationArchiveService
{
    private readonly ILogger<AllocationArchiveService>? _logger;
    private readonly string _archiveDirectory;
    private List<ArchiveInfo> _archives = new();

    public IReadOnlyList<ArchiveInfo> Archives => _archives.AsReadOnly();

    public AllocationArchiveService(ILogger<AllocationArchiveService>? logger = null)
    {
        _logger = logger;
        _archiveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SAP", "Archives", "AllocationBuddy");
        Directory.CreateDirectory(_archiveDirectory);
    }

    public async Task LoadArchivesAsync()
    {
        try
        {
            var archives = new List<ArchiveInfo>();
            
            if (Directory.Exists(_archiveDirectory))
            {
                var files = Directory.GetFiles(_archiveDirectory, "*.json")
                    .OrderByDescending(f => File.GetCreationTime(f));

                foreach (var file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        var id = Path.GetFileNameWithoutExtension(file);
                        
                        // Try to read metadata from the file
                        var json = await File.ReadAllTextAsync(file);
                        var metadata = JsonSerializer.Deserialize<ArchiveMetadata>(json);
                        
                        archives.Add(new ArchiveInfo
                        {
                            Id = id,
                            FilePath = file,
                            CreatedAt = info.CreationTime,
                            DisplayName = metadata?.SessionName ?? info.CreationTime.ToString("yyyy-MM-dd HH:mm"),
                            LocationCount = metadata?.LocationCount ?? 0,
                            ItemCount = metadata?.ItemCount ?? 0
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to read archive: {File}", file);
                    }
                }
            }

            _archives = archives;
            _logger?.LogInformation("Loaded {Count} archives", _archives.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load archives");
        }
    }

    public async Task<ArchiveInfo?> CreateArchiveAsync<TLocation, TItem>(
        IEnumerable<TLocation> locationAllocations,
        IEnumerable<TItem> itemPool,
        string? sessionName = null)
    {
        try
        {
            var locations = locationAllocations.ToList();
            var items = itemPool.ToList();
            
            // Don't archive empty sessions
            if (locations.Count == 0 && items.Count == 0)
            {
                _logger?.LogDebug("Skipping archive of empty session");
                return null;
            }

            var timestamp = DateTime.Now;
            var id = $"archive_{timestamp:yyyyMMdd_HHmmss}";
            var filePath = Path.Combine(_archiveDirectory, $"{id}.json");

            var session = new ArchivedSession<TLocation, TItem>
            {
                LocationAllocations = locations,
                ItemPool = items,
                ArchivedAt = timestamp,
                SessionName = sessionName
            };

            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(filePath, json);

            var archiveInfo = new ArchiveInfo
            {
                Id = id,
                FilePath = filePath,
                CreatedAt = timestamp,
                DisplayName = sessionName ?? timestamp.ToString("yyyy-MM-dd HH:mm"),
                LocationCount = locations.Count,
                ItemCount = items.Count
            };

            _archives.Insert(0, archiveInfo);
            _logger?.LogInformation("Created archive: {Id} with {Locations} locations, {Items} items", 
                id, locations.Count, items.Count);

            return archiveInfo;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create archive");
            return null;
        }
    }

    public async Task<ArchivedSession<TLocation, TItem>?> LoadArchiveAsync<TLocation, TItem>(ArchiveInfo archive)
    {
        try
        {
            if (!File.Exists(archive.FilePath))
            {
                _logger?.LogWarning("Archive file not found: {Path}", archive.FilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(archive.FilePath);
            var session = JsonSerializer.Deserialize<ArchivedSession<TLocation, TItem>>(json);
            
            _logger?.LogInformation("Loaded archive: {Id}", archive.Id);
            return session;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load archive: {Id}", archive.Id);
            return null;
        }
    }

    public Task<bool> DeleteArchiveAsync(ArchiveInfo archive)
    {
        try
        {
            if (File.Exists(archive.FilePath))
            {
                File.Delete(archive.FilePath);
                _archives.Remove(archive);
                _logger?.LogInformation("Deleted archive: {Id}", archive.Id);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete archive: {Id}", archive.Id);
            return Task.FromResult(false);
        }
    }

    public ArchiveInfo? GetMostRecentArchive()
    {
        // === OPTIMIZATION: MaxBy avoids full sort ===
        return _archives.Count == 0 ? null : _archives.MaxBy(a => a.CreatedAt);
    }
}
