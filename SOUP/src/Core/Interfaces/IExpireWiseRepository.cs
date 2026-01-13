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
}
