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
                try { transaction.Rollback(); } catch { }
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
}
