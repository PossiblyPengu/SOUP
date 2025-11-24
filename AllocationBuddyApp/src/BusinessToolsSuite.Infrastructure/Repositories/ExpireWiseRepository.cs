using BusinessToolsSuite.Core.Entities.ExpireWise;
using BusinessToolsSuite.Infrastructure.Data;

namespace BusinessToolsSuite.Infrastructure.Repositories;

/// <summary>
/// ExpireWise repository implementation
/// </summary>
public class ExpireWiseRepository : LiteDbRepository<ExpirationItem>, IExpireWiseRepository
{
    public ExpireWiseRepository(LiteDbContext context, ILogger<ExpireWiseRepository>? logger = null)
        : base(context, logger)
    {
    }

    public Task<IReadOnlyList<ExpirationItem>> GetExpiringItemsAsync(
        int daysThreshold,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var threshold = DateTime.UtcNow.AddDays(daysThreshold);
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted && x.ExpiryDate <= threshold && x.ExpiryDate >= DateTime.UtcNow)
                .ToList();
            return Task.FromResult<IReadOnlyList<ExpirationItem>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting expiring items within {Days} days", daysThreshold);
            throw;
        }
    }

    public Task<IReadOnlyList<ExpirationItem>> GetExpiredItemsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted && x.ExpiryDate < DateTime.UtcNow)
                .ToList();
            return Task.FromResult<IReadOnlyList<ExpirationItem>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting expired items");
            throw;
        }
    }

    public Task<IReadOnlyList<ExpirationItem>> GetItemsByStatusAsync(
        ExpirationStatus status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var all = Collection
                .Query()
                .Where(x => !x.IsDeleted)
                .ToList();

            var filtered = all.Where(x => x.Status == status).ToList();
            return Task.FromResult<IReadOnlyList<ExpirationItem>>(filtered);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting items by status: {Status}", status);
            throw;
        }
    }

    public Task<IReadOnlyList<ExpirationItem>> GetItemsByLocationAsync(
        string location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted && x.Location == location)
                .ToList();
            return Task.FromResult<IReadOnlyList<ExpirationItem>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting items by location: {Location}", location);
            throw;
        }
    }

    public Task<Dictionary<ExpirationStatus, int>> GetStatusSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var all = Collection
                .Query()
                .Where(x => !x.IsDeleted)
                .ToList();

            var summary = all
                .GroupBy(x => x.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            // Ensure all statuses are present
            foreach (ExpirationStatus status in Enum.GetValues<ExpirationStatus>())
            {
                summary.TryAdd(status, 0);
            }

            return Task.FromResult(summary);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting status summary");
            throw;
        }
    }
}
