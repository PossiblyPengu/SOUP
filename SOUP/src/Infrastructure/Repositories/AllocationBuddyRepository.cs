using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SOUP.Core.Common;
using SOUP.Core.Entities.AllocationBuddy;
using SOUP.Core.Interfaces;
using SOUP.Infrastructure.Data;

namespace SOUP.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Allocation Buddy entries
/// </summary>
public class AllocationBuddyRepository : SqliteRepository<AllocationEntry>, IAllocationBuddyRepository
{
    private const string ArchiveTableName = "AllocationArchive";

    private static readonly JsonSerializerOptions ArchiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public AllocationBuddyRepository(SqliteDbContext context, ILogger<SqliteRepository<AllocationEntry>>? logger = null)
        : base(context, logger)
    {
        // Ensure archive table exists
        Context.EnsureTable<AllocationArchive>(ArchiveTableName);
    }

    public async Task<IEnumerable<AllocationEntry>> GetByArchiveIdAsync(Guid archiveId)
    {
        return await FindAsync(e => e.ArchiveId == archiveId && !e.IsDeleted);
    }

    public async Task<IEnumerable<AllocationEntry>> GetActiveEntriesAsync()
    {
        return await FindAsync(e => e.ArchiveId == null && !e.IsDeleted);
    }

    public Task<IEnumerable<AllocationArchive>> GetAllArchivesAsync()
    {
        using var connection = Context.CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Data FROM [{ArchiveTableName}] WHERE IsDeleted = 0";

        var archives = new List<AllocationArchive>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var json = reader.GetString(0);
            var archive = JsonSerializer.Deserialize<AllocationArchive>(json, ArchiveJsonOptions);
            if (archive is not null)
            {
                archives.Add(archive);
            }
        }

        return Task.FromResult<IEnumerable<AllocationArchive>>(archives);
    }

    public Task<AllocationArchive> CreateArchiveAsync(string name, string? notes = null)
    {
        var archive = new AllocationArchive
        {
            Name = name,
            Notes = notes,
            ArchivedAt = DateTime.UtcNow
        };

        using var connection = Context.CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO [{ArchiveTableName}] (Id, CreatedAt, UpdatedAt, IsDeleted, Data)
            VALUES (@Id, @CreatedAt, @UpdatedAt, @IsDeleted, @Data)
        ";
        cmd.Parameters.AddWithValue("@Id", archive.Id.ToString());
        cmd.Parameters.AddWithValue("@CreatedAt", archive.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@UpdatedAt", DBNull.Value);
        cmd.Parameters.AddWithValue("@IsDeleted", 0);
        cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(archive, ArchiveJsonOptions));
        cmd.ExecuteNonQuery();

        return Task.FromResult(archive);
    }

    public async Task ArchiveEntriesAsync(Guid archiveId, IEnumerable<Guid> entryIds)
    {
        var entryIdSet = entryIds.ToHashSet();
        var entries = (await GetAllAsync()).Where(e => entryIdSet.Contains(e.Id)).ToList();

        foreach (var entry in entries)
        {
            entry.ArchiveId = archiveId;
            entry.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(entry);
        }

        // Update archive entry count
        using var connection = Context.CreateConnection();
        connection.Open();

        AllocationArchive? archive = null;
        using (var getCmd = connection.CreateCommand())
        {
            getCmd.CommandText = $"SELECT Data FROM [{ArchiveTableName}] WHERE Id = @Id";
            getCmd.Parameters.AddWithValue("@Id", archiveId.ToString());
            var json = getCmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(json))
            {
                archive = JsonSerializer.Deserialize<AllocationArchive>(json, ArchiveJsonOptions);
            }
        }

        if (archive != null)
        {
            archive.EntryCount = entries.Count;
            archive.UpdatedAt = DateTime.UtcNow;

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = $@"
                UPDATE [{ArchiveTableName}]
                SET UpdatedAt = @UpdatedAt, Data = @Data
                WHERE Id = @Id
            ";
            updateCmd.Parameters.AddWithValue("@Id", archiveId.ToString());
            updateCmd.Parameters.AddWithValue("@UpdatedAt", archive.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
            updateCmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(archive, ArchiveJsonOptions));
            updateCmd.ExecuteNonQuery();
        }
    }
}
