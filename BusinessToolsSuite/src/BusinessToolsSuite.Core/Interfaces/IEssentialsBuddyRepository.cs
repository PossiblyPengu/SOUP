using BusinessToolsSuite.Core.Entities.EssentialsBuddy;

namespace BusinessToolsSuite.Core.Interfaces;

/// <summary>
/// Repository interface for Essentials Buddy inventory items
/// </summary>
public interface IEssentialsBuddyRepository : IRepository<InventoryItem>
{
    Task<IReadOnlyList<InventoryItem>> GetByStatusAsync(
        InventoryStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItem>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItem>> GetByBinCodeAsync(
        string binCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItem>> GetLowStockItemsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItem>> GetOutOfStockItemsAsync(
        CancellationToken cancellationToken = default);

    Task<Dictionary<InventoryStatus, int>> GetStatusSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalInventoryValueAsync(
        CancellationToken cancellationToken = default);
}
