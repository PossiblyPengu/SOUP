using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SOUP.Core;

namespace SOUP.Services;

/// <summary>
/// Service for exporting and importing SQLite database backups.
/// Supports backing up all databases to a single ZIP archive.
/// </summary>
public class DatabaseBackupService
{
    private readonly ILogger<DatabaseBackupService>? _logger;

    // Database files to include in backup
    private static readonly (string Path, string ArchiveName)[] DatabaseFiles =
    {
        (AppPaths.MainDbPath, "SOUP.db"),
        (AppPaths.DictionaryDbPath, "dictionaries.db"),
        (AppPaths.OrderLogDbPath, "orders.db"),
    };

    public DatabaseBackupService(ILogger<DatabaseBackupService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Exports all databases to a ZIP archive.
    /// </summary>
    /// <param name="exportPath">The path for the output ZIP file.</param>
    /// <returns>Number of databases exported.</returns>
    public async Task<(int Count, string[] Files)> ExportDatabasesAsync(string exportPath)
    {
        if (string.IsNullOrWhiteSpace(exportPath))
            throw new ArgumentNullException(nameof(exportPath));

        var exportedFiles = new System.Collections.Generic.List<string>();

        try
        {
            // Ensure parent directory exists
            var directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Delete existing file if present
            if (File.Exists(exportPath))
                File.Delete(exportPath);

            using var archive = ZipFile.Open(exportPath, ZipArchiveMode.Create);

            foreach (var (dbPath, archiveName) in DatabaseFiles)
            {
                if (!File.Exists(dbPath))
                {
                    _logger?.LogDebug("Skipping {Database} - file does not exist", archiveName);
                    continue;
                }

                try
                {
                    // SQLite uses WAL mode, so we need to checkpoint before backup
                    await CheckpointDatabaseAsync(dbPath);

                    // Copy database to archive
                    archive.CreateEntryFromFile(dbPath, archiveName);
                    exportedFiles.Add(archiveName);
                    _logger?.LogDebug("Added {Database} to backup archive", archiveName);

                    // Also backup WAL and SHM files if they exist (for completeness)
                    var walPath = dbPath + "-wal";
                    var shmPath = dbPath + "-shm";

                    if (File.Exists(walPath))
                    {
                        archive.CreateEntryFromFile(walPath, archiveName + "-wal");
                        _logger?.LogDebug("Added {Database}-wal to backup archive", archiveName);
                    }
                }
                catch (IOException ex)
                {
                    _logger?.LogWarning(ex, "Could not backup {Database} - file may be in use", archiveName);
                    // Continue with other databases
                }
            }

            // Add metadata file
            var metadata = new
            {
                ExportDate = DateTime.UtcNow,
                AppVersion = AppVersion.Version,
                Databases = exportedFiles.ToArray()
            };
            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var metadataEntry = archive.CreateEntry("backup-info.json");
            using (var writer = new StreamWriter(metadataEntry.Open()))
            {
                await writer.WriteAsync(metadataJson);
            }

            _logger?.LogInformation("Exported {Count} databases to {Path}", exportedFiles.Count, exportPath);
            return (exportedFiles.Count, exportedFiles.ToArray());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export databases to {Path}", exportPath);
            throw;
        }
    }

    /// <summary>
    /// Imports databases from a ZIP archive backup.
    /// </summary>
    /// <param name="importPath">The path to the ZIP archive.</param>
    /// <param name="overwrite">Whether to overwrite existing databases.</param>
    /// <returns>Number of databases imported.</returns>
    public async Task<(int Count, string[] Files)> ImportDatabasesAsync(string importPath, bool overwrite = true)
    {
        if (string.IsNullOrWhiteSpace(importPath))
            throw new ArgumentNullException(nameof(importPath));

        if (!File.Exists(importPath))
            throw new FileNotFoundException("Backup file not found", importPath);

        var importedFiles = new System.Collections.Generic.List<string>();

        try
        {
            using var archive = ZipFile.OpenRead(importPath);

            foreach (var (dbPath, archiveName) in DatabaseFiles)
            {
                var entry = archive.GetEntry(archiveName);
                if (entry == null)
                {
                    _logger?.LogDebug("Skipping {Database} - not found in archive", archiveName);
                    continue;
                }

                try
                {
                    // Ensure target directory exists
                    var directory = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    // Handle existing file
                    if (File.Exists(dbPath))
                    {
                        if (!overwrite)
                        {
                            _logger?.LogDebug("Skipping {Database} - file exists and overwrite=false", archiveName);
                            continue;
                        }

                        // Create backup of existing file
                        var backupPath = dbPath + $".backup-{DateTime.Now:yyyyMMdd-HHmmss}";
                        File.Move(dbPath, backupPath);
                        _logger?.LogDebug("Backed up existing {Database} to {BackupPath}", archiveName, backupPath);

                        // Also remove WAL/SHM files
                        var walPath = dbPath + "-wal";
                        var shmPath = dbPath + "-shm";
                        if (File.Exists(walPath)) File.Delete(walPath);
                        if (File.Exists(shmPath)) File.Delete(shmPath);
                    }

                    // Extract database
                    entry.ExtractToFile(dbPath);
                    importedFiles.Add(archiveName);
                    _logger?.LogDebug("Restored {Database} from backup archive", archiveName);

                    // Also restore WAL if present in archive
                    var walEntry = archive.GetEntry(archiveName + "-wal");
                    if (walEntry != null)
                    {
                        walEntry.ExtractToFile(dbPath + "-wal");
                        _logger?.LogDebug("Restored {Database}-wal from backup archive", archiveName);
                    }
                }
                catch (IOException ex)
                {
                    _logger?.LogWarning(ex, "Could not restore {Database} - file may be in use", archiveName);
                    // Continue with other databases
                }
            }

            _logger?.LogInformation("Imported {Count} databases from {Path}", importedFiles.Count, importPath);
            return (importedFiles.Count, importedFiles.ToArray());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import databases from {Path}", importPath);
            throw;
        }
    }

    /// <summary>
    /// Gets backup information from a ZIP archive without importing.
    /// </summary>
    public BackupInfo? GetBackupInfo(string archivePath)
    {
        if (!File.Exists(archivePath))
            return null;

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var databases = new System.Collections.Generic.List<string>();

            foreach (var (_, archiveName) in DatabaseFiles)
            {
                if (archive.GetEntry(archiveName) != null)
                    databases.Add(archiveName);
            }

            // Try to read metadata
            var metadataEntry = archive.GetEntry("backup-info.json");
            if (metadataEntry != null)
            {
                using var reader = new StreamReader(metadataEntry.Open());
                var json = reader.ReadToEnd();
                var metadata = System.Text.Json.JsonSerializer.Deserialize<BackupMetadata>(json);

                return new BackupInfo
                {
                    FilePath = archivePath,
                    FileSize = new FileInfo(archivePath).Length,
                    ExportDate = metadata?.ExportDate,
                    AppVersion = metadata?.AppVersion,
                    Databases = databases.ToArray()
                };
            }

            return new BackupInfo
            {
                FilePath = archivePath,
                FileSize = new FileInfo(archivePath).Length,
                Databases = databases.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not read backup info from {Path}", archivePath);
            return null;
        }
    }

    /// <summary>
    /// Checkpoints a WAL-mode SQLite database to ensure all data is written to the main file.
    /// </summary>
    private async Task CheckpointDatabaseAsync(string dbPath)
    {
        try
        {
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared
            }.ToString();

            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteNonQueryAsync();

            _logger?.LogDebug("Checkpointed database {Path}", dbPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not checkpoint database {Path}", dbPath);
            // Continue anyway - backup will still work, may just be slightly inconsistent
        }
    }

    /// <summary>
    /// Generates a suggested backup filename with timestamp.
    /// </summary>
    public static string GenerateBackupFileName()
    {
        return $"SOUP-Backup-{DateTime.Now:yyyy-MM-dd-HHmmss}.zip";
    }

    private class BackupMetadata
    {
        public DateTime? ExportDate { get; set; }
        public string? AppVersion { get; set; }
        public string[]? Databases { get; set; }
    }
}

/// <summary>
/// Information about a backup archive.
/// </summary>
public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime? ExportDate { get; set; }
    public string? AppVersion { get; set; }
    public string[] Databases { get; set; } = Array.Empty<string>();

    public string FileSizeFormatted => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        _ => $"{FileSize / (1024.0 * 1024.0):F2} MB"
    };
}
