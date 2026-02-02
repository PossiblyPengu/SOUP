using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Core.Interfaces;
using SOUP.Infrastructure.Data;

namespace SOUP.Infrastructure.Repositories;

/// <summary>
/// ExpireWise repository implementation
/// </summary>
public class ExpireWiseRepository : SqliteRepository<ExpirationItem>, IExpireWiseRepository
{
    public ExpireWiseRepository(SqliteDbContext context, ILogger<ExpireWiseRepository>? logger = null)
        : base(context, logger)
    {
        // Ensure archive table exists
        context.EnsureTable<ArchivedExpirationItem>();
    }

    /// <summary>
    /// Replaces all items in the ExpirationItem table with the provided collection.
    /// Uses a transaction to ensure all-or-nothing semantics to prevent partial imports.
    /// </summary>
    public async Task<bool> ReplaceAllAsync(List<ExpirationItem> newItems)
    {
            try
            {
                ArgumentNullException.ThrowIfNull(newItems);

            using var connection = Context.CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // Delete existing rows
                using (var delCmd = connection.CreateCommand())
                {
                    delCmd.Transaction = transaction;
                    delCmd.CommandText = $"DELETE FROM [{typeof(ExpirationItem).Name}]";
                    delCmd.ExecuteNonQuery();
                }

                // Insert new items
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = false };
                foreach (var item in newItems)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = $@"
                        INSERT INTO [{typeof(ExpirationItem).Name}] (Id, CreatedAt, UpdatedAt, IsDeleted, Data)
                        VALUES (@Id, @CreatedAt, @UpdatedAt, @IsDeleted, @Data)
                    ";
                    cmd.Parameters.AddWithValue("@Id", item.Id.ToString());
                    cmd.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("O"));
                    cmd.Parameters.AddWithValue("@UpdatedAt", item.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsDeleted", item.IsDeleted ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(item, options));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                Logger?.LogInformation("Replaced all ExpireWise items with {Count} new items", newItems.Count);
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); } catch (Exception rbEx) { Logger?.LogWarning(rbEx, "Failed to rollback transaction"); }
                Logger?.LogError(ex, "Failed to replace ExpireWise items (rolled back)");
                return await Task.FromResult(false);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "ReplaceAllAsync failed");
            throw;
        }
    }

    public async Task<IEnumerable<ExpirationItem>> GetExpiredItemsAsync()
    {
        try
        {
            var results = await FindAsync(x => !x.IsDeleted && x.ExpiryDate < DateTime.UtcNow);
            return results;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting expired items");
            throw;
        }
    }

    public async Task<IEnumerable<ExpirationItem>> GetExpiringSoonAsync(int days = 7)
    {
        try
        {
            var threshold = DateTime.UtcNow.AddDays(days);
            var results = await FindAsync(x => !x.IsDeleted && x.ExpiryDate <= threshold && x.ExpiryDate >= DateTime.UtcNow);
            return results;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting items expiring within {Days} days", days);
            throw;
        }
    }

    public async Task<IEnumerable<ExpirationItem>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        try
        {
            var results = await FindAsync(x => !x.IsDeleted && x.ExpiryDate >= start && x.ExpiryDate <= end);
            return results;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting items by date range: {Start} - {End}", start, end);
            throw;
        }
    }

    public async Task<int> ArchiveExpiredItemsAsync(string? storeLocation = null)
    {
        try
        {
            using var connection = Context.CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var today = DateTime.Today;
                var expiredItems = await FindAsync(x => !x.IsDeleted && x.ExpiryDate < today);
                
                if (!string.IsNullOrEmpty(storeLocation))
                {
                    expiredItems = expiredItems.Where(x => x.Location == storeLocation).ToList();
                }

                if (!expiredItems.Any())
                {
                    transaction.Commit();
                    return 0;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = false };
                var archivedDate = DateTime.UtcNow;
                int count = 0;

                foreach (var item in expiredItems)
                {
                    // Create archived item
                    var archived = new ArchivedExpirationItem
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = item.CreatedAt,
                        UpdatedAt = item.UpdatedAt,
                        ItemNumber = item.ItemNumber,
                        Upc = item.Upc,
                        Description = item.Description,
                        Location = item.Location,
                        Units = item.Units,
                        ExpiryDate = item.ExpiryDate,
                        Notes = item.Notes,
                        Category = item.Category,
                        ArchivedDate = archivedDate
                    };

                    // Insert into archive table
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = $@"
                            INSERT INTO [{typeof(ArchivedExpirationItem).Name}] (Id, CreatedAt, UpdatedAt, IsDeleted, Data)
                            VALUES (@Id, @CreatedAt, @UpdatedAt, @IsDeleted, @Data)
                        ";
                        cmd.Parameters.AddWithValue("@Id", archived.Id.ToString());
                        cmd.Parameters.AddWithValue("@CreatedAt", archived.CreatedAt.ToString("O"));
                        cmd.Parameters.AddWithValue("@UpdatedAt", archived.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsDeleted", archived.IsDeleted ? 1 : 0);
                        cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(archived, options));
                        cmd.ExecuteNonQuery();
                    }

                    // Delete from active table
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = $"DELETE FROM [{typeof(ExpirationItem).Name}] WHERE Id = @Id";
                        cmd.Parameters.AddWithValue("@Id", item.Id.ToString());
                        cmd.ExecuteNonQuery();
                    }

                    count++;
                }

                transaction.Commit();
                Logger?.LogInformation("Archived {Count} expired items{Store}", count, 
                    string.IsNullOrEmpty(storeLocation) ? "" : $" from {storeLocation}");
                return count;
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); } catch (Exception rbEx) { Logger?.LogWarning(rbEx, "Failed to rollback transaction"); }
                Logger?.LogError(ex, "Failed to archive expired items (rolled back)");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "ArchiveExpiredItemsAsync failed");
            throw;
        }
    }

    public async Task<IEnumerable<ArchivedExpirationItem>> GetArchivedItemsAsync(string? storeLocation = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            using var connection = Context.CreateConnection();
            connection.Open();

            var query = $"SELECT Data FROM [{typeof(ArchivedExpirationItem).Name}] WHERE IsDeleted = 0";
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = query;
            
            var results = new List<ArchivedExpirationItem>();
            using var reader = cmd.ExecuteReader();
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            while (reader.Read())
            {
                var json = reader.GetString(0);
                var item = JsonSerializer.Deserialize<ArchivedExpirationItem>(json, options);
                if (item != null)
                {
                    // Apply filters
                    if (!string.IsNullOrEmpty(storeLocation) && item.Location != storeLocation)
                        continue;
                    if (startDate.HasValue && item.ArchivedDate < startDate.Value)
                        continue;
                    if (endDate.HasValue && item.ArchivedDate > endDate.Value)
                        continue;
                    
                    results.Add(item);
                }
            }

            return await Task.FromResult(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting archived items");
            throw;
        }
    }

    public async Task<int> DeleteOldArchivedItemsAsync(DateTime olderThan)
    {
        try
        {
            using var connection = Context.CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                DELETE FROM [{typeof(ArchivedExpirationItem).Name}]
                WHERE json_extract(Data, '$.ArchivedDate') < @OlderThan
            ";
            cmd.Parameters.AddWithValue("@OlderThan", olderThan.ToString("O"));
            
            var count = cmd.ExecuteNonQuery();
            Logger?.LogInformation("Deleted {Count} archived items older than {Date}", count, olderThan);
            return await Task.FromResult(count);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error deleting old archived items");
            throw;
        }
    }
}
