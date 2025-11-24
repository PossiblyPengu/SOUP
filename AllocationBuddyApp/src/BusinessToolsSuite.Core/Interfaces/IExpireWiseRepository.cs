using BusinessToolsSuite.Core.Entities.ExpireWise;

namespace BusinessToolsSuite.Core.Interfaces;

/// <summary>
/// Repository interface for ExpireWise expiration items
/// </summary>
public interface IExpireWiseRepository : IRepository<ExpirationItem>
{
    Task<IReadOnlyList<ExpirationItem>> GetExpiringItemsAsync(
        int daysThreshold,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpirationItem>> GetExpiredItemsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpirationItem>> GetItemsByStatusAsync(
        ExpirationStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpirationItem>> GetItemsByLocationAsync(
        string location,
        CancellationToken cancellationToken = default);

    Task<Dictionary<ExpirationStatus, int>> GetStatusSummaryAsync(
        CancellationToken cancellationToken = default);
}
