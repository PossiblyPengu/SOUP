using BusinessToolsSuite.Core.Entities.EssentialsBuddy;
using BusinessToolsSuite.Core.Interfaces;
using BusinessToolsSuite.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace BusinessToolsSuite.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Essentials Buddy inventory items
/// </summary>
public class EssentialsBuddyRepository : LiteDbRepository<InventoryItem>, IEssentialsBuddyRepository
{
    public EssentialsBuddyRepository(LiteDbContext context, ILogger<LiteDbRepository<InventoryItem>>? logger = null)
        : base(context, logger)
    {
    }

    public async Task<IReadOnlyList<InventoryItem>> GetByStatusAsync(
        InventoryStatus status,
        CancellationToken cancellationToken = default)
    {
        var allItems = await GetAllAsync(cancellationToken);
        return allItems.Where(i => i.Status == status).ToList();
    }

    public async Task<IReadOnlyList<InventoryItem>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(i => i.Category == category && !i.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryItem>> GetByBinCodeAsync(
        string binCode,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(i => i.BinCode == binCode && !i.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryItem>> GetLowStockItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var allItems = await GetAllAsync(cancellationToken);
        return allItems.Where(i => i.Status == InventoryStatus.Low).ToList();
    }

    public async Task<IReadOnlyList<InventoryItem>> GetOutOfStockItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var allItems = await GetAllAsync(cancellationToken);
        return allItems.Where(i => i.Status == InventoryStatus.OutOfStock).ToList();
    }

    public async Task<Dictionary<InventoryStatus, int>> GetStatusSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var allItems = await GetAllAsync(cancellationToken);

        return allItems
            .GroupBy(i => i.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<decimal> GetTotalInventoryValueAsync(
        CancellationToken cancellationToken = default)
    {
        var allItems = await GetAllAsync(cancellationToken);

        return allItems
            .Where(i => i.UnitCost.HasValue)
            .Sum(i => i.QuantityOnHand * (i.UnitCost ?? 0));
    }
}
