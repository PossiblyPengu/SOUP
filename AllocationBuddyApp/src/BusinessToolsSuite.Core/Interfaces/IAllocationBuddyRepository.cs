using BusinessToolsSuite.Core.Entities.AllocationBuddy;

namespace BusinessToolsSuite.Core.Interfaces;

/// <summary>
/// Repository interface for Allocation Buddy entries
/// </summary>
public interface IAllocationBuddyRepository : IRepository<AllocationEntry>
{
    Task<IReadOnlyList<AllocationEntry>> GetByStoreIdAsync(
        string storeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllocationEntry>> GetByRankAsync(
        StoreRank rank,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllocationEntry>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllocationEntry>> GetByItemNumberAsync(
        string itemNumber,
        CancellationToken cancellationToken = default);

    Task<Dictionary<StoreRank, int>> GetRankSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, decimal>> GetTotalsByStoreAsync(
        CancellationToken cancellationToken = default);
}
