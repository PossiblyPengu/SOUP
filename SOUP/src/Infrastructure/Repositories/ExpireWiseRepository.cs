using System;
using System.Collections.Generic;
using System.Linq;
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
