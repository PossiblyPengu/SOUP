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

    /// <summary>
    /// Gets the master list of essential items.
    /// </summary>
    /// <returns>A collection of master list items.</returns>
    Task<IEnumerable<MasterListItem>> GetMasterListAsync();

    /// <summary>
    /// Updates the master list of essential items.
    /// </summary>
    /// <param name="items">The updated master list items.</param>
    Task UpdateMasterListAsync(IEnumerable<MasterListItem> items);
}
