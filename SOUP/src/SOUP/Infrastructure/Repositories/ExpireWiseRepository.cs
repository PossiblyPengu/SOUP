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
public class ExpireWiseRepository : LiteDbRepository<ExpirationItem>, IExpireWiseRepository
{
    public ExpireWiseRepository(LiteDbContext context, ILogger<ExpireWiseRepository>? logger = null)
        : base(context, logger)
    {
    }

    public Task<IEnumerable<ExpirationItem>> GetExpiredItemsAsync()
    {
        try
        {
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted && x.ExpiryDate < DateTime.UtcNow)
                .ToList();
            return Task.FromResult<IEnumerable<ExpirationItem>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting expired items");
            throw;
        }
    }

    public Task<IEnumerable<ExpirationItem>> GetExpiringSoonAsync(int days = 7)
    {
        try
        {
            var threshold = DateTime.UtcNow.AddDays(days);
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted && x.ExpiryDate <= threshold && x.ExpiryDate >= DateTime.UtcNow)
                .ToList();
            return Task.FromResult<IEnumerable<ExpirationItem>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting items expiring within {Days} days", days);
            throw;
        }
    }

    public Task<IEnumerable<ExpirationItem>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        try
        {
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted && x.ExpiryDate >= start && x.ExpiryDate <= end)
                .ToList();
            return Task.FromResult<IEnumerable<ExpirationItem>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting items by date range: {Start} - {End}", start, end);
            throw;
        }
    }
}
