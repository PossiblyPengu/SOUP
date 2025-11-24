using BusinessToolsSuite.Core.Entities.AllocationBuddy;
using BusinessToolsSuite.Core.Interfaces;
using BusinessToolsSuite.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace BusinessToolsSuite.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Allocation Buddy entries
/// </summary>
public class AllocationBuddyRepository : LiteDbRepository<AllocationEntry>, IAllocationBuddyRepository
{
    public AllocationBuddyRepository(LiteDbContext context, ILogger<LiteDbRepository<AllocationEntry>>? logger = null)
        : base(context, logger)
    {
    }

    public async Task<IReadOnlyList<AllocationEntry>> GetByStoreIdAsync(
        string storeId,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(e => e.StoreId == storeId && !e.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<AllocationEntry>> GetByRankAsync(
        StoreRank rank,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(e => e.Rank == rank && !e.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<AllocationEntry>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(e => e.Category == category && !e.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<AllocationEntry>> GetByItemNumberAsync(
        string itemNumber,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(e => e.ItemNumber == itemNumber && !e.IsDeleted, cancellationToken);
    }

    public async Task<Dictionary<StoreRank, int>> GetRankSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var allEntries = await GetAllAsync(cancellationToken);
        var activeEntries = allEntries.Where(e => !e.IsDeleted);

        return activeEntries
            .GroupBy(e => e.Rank)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, decimal>> GetTotalsByStoreAsync(
        CancellationToken cancellationToken = default)
    {
        var allEntries = await GetAllAsync(cancellationToken);
        var activeEntries = allEntries.Where(e => !e.IsDeleted && e.UnitPrice.HasValue);

        return activeEntries
            .GroupBy(e => e.StoreId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => e.Quantity * (e.UnitPrice ?? 0)));
    }
}
