using System.Collections.Generic;
using System.Threading.Tasks;
using SOUP.Core.Entities.EssentialsBuddy;

namespace SOUP.Core.Interfaces;

/// <summary>
/// Repository interface for EssentialsBuddy-specific data operations.
/// </summary>
public interface IEssentialsBuddyRepository : IRepository<InventoryItem>
{
    /// <summary>
    /// Gets all items with quantity below their minimum threshold.
    /// </summary>
    /// <returns>A collection of low-stock items.</returns>
    Task<IEnumerable<InventoryItem>> GetItemsBelowThresholdAsync();
}
