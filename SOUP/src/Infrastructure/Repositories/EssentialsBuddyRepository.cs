using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.EssentialsBuddy;
using SOUP.Core.Interfaces;
using SOUP.Infrastructure.Data;

namespace SOUP.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Essentials Buddy inventory items
/// </summary>
public class EssentialsBuddyRepository : SqliteRepository<InventoryItem>, IEssentialsBuddyRepository
{
    private const string MasterListTableName = "MasterListItem";

    private static readonly JsonSerializerOptions MasterListJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public EssentialsBuddyRepository(SqliteDbContext context, ILogger<SqliteRepository<InventoryItem>>? logger = null)
        : base(context, logger)
    {
        // Ensure master list table exists
        Context.EnsureTable<MasterListItem>(MasterListTableName);
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsBelowThresholdAsync()
    {
        var allItems = await GetAllAsync();
        return allItems.Where(i => i.IsBelowThreshold);
    }

    public Task<IEnumerable<MasterListItem>> GetMasterListAsync()
    {
        using var connection = Context.CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT Data FROM [{MasterListTableName}] WHERE IsDeleted = 0";

        var items = new List<MasterListItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var json = reader.GetString(0);
            var item = JsonSerializer.Deserialize<MasterListItem>(json, MasterListJsonOptions);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return Task.FromResult<IEnumerable<MasterListItem>>(items);
    }

    public Task UpdateMasterListAsync(IEnumerable<MasterListItem> items)
    {
        using var connection = Context.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var item in items)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = $@"
                    INSERT INTO [{MasterListTableName}] (Id, CreatedAt, UpdatedAt, IsDeleted, Data)
                    VALUES (@Id, @CreatedAt, @UpdatedAt, @IsDeleted, @Data)
                    ON CONFLICT(Id) DO UPDATE SET
                        UpdatedAt = excluded.UpdatedAt,
                        IsDeleted = excluded.IsDeleted,
                        Data = excluded.Data
                ";
                cmd.Parameters.AddWithValue("@Id", item.Id.ToString());
                cmd.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("O"));
                cmd.Parameters.AddWithValue("@UpdatedAt", item.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@IsDeleted", item.IsDeleted ? 1 : 0);
                cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(item, MasterListJsonOptions));
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return Task.CompletedTask;
    }
}
