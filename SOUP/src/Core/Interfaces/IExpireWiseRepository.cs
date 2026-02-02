using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SOUP.Core.Entities.ExpireWise;

namespace SOUP.Core.Interfaces;

/// <summary>
/// Repository interface for ExpireWise-specific data operations.
/// </summary>
public interface IExpireWiseRepository : IRepository<ExpirationItem>
{
    /// <summary>
    /// Gets all items that have already expired.
    /// </summary>
    /// <returns>A collection of expired items.</returns>
    Task<IEnumerable<ExpirationItem>> GetExpiredItemsAsync();

    /// <summary>
    /// Gets items expiring within the specified number of days.
    /// </summary>
    /// <param name="days">Number of days to look ahead (default: 7).</param>
    /// <returns>A collection of items expiring soon.</returns>
    Task<IEnumerable<ExpirationItem>> GetExpiringSoonAsync(int days = 7);

    /// <summary>
    /// Gets items with expiration dates within a date range.
    /// </summary>
    /// <param name="start">Start date (inclusive).</param>
    /// <param name="end">End date (inclusive).</param>
    /// <returns>A collection of items within the date range.</returns>
    Task<IEnumerable<ExpirationItem>> GetByDateRangeAsync(DateTime start, DateTime end);

    /// <summary>
    /// Replace all items in the repository with the provided collection in a single transaction.
    /// Returns true on success, false on failure (transaction rolled back).
    /// </summary>
    Task<bool> ReplaceAllAsync(List<ExpirationItem> newItems);

    /// <summary>
    /// Archives expired items by moving them to the archive table and removing from active items.
    /// </summary>
    /// <param name="storeLocation">Optional store location filter. If null, archives all expired items.</param>
    /// <returns>Number of items archived.</returns>
    Task<int> ArchiveExpiredItemsAsync(string? storeLocation = null);

    /// <summary>
    /// Gets archived items for a specific store.
    /// </summary>
    /// <param name="storeLocation">Store location to filter by.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <returns>A collection of archived items.</returns>
    Task<IEnumerable<ArchivedExpirationItem>> GetArchivedItemsAsync(string? storeLocation = null, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Deletes archived items older than the specified date.
    /// </summary>
    /// <param name="olderThan">Delete archived items older than this date.</param>
    /// <returns>Number of items deleted.</returns>
    Task<int> DeleteOldArchivedItemsAsync(DateTime olderThan);
}
