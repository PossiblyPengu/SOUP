using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for synchronizing timestamps across linked order groups.
/// Ensures all items in a linked group maintain consistent timing data.
/// </summary>
public class OrderTimestampSyncService
{
    /// <summary>
    /// Synchronizes timestamps across all items in a linked group.
    /// Uses the first item as the reference source for timestamp data.
    /// </summary>
    /// <param name="items">Items in the linked group to synchronize</param>
    public void SyncLinkedGroupTimestamps(IReadOnlyList<OrderItem> items)
    {
        if (items == null || items.Count <= 1)
            return;

        var referenceItem = items[0];

        // Sync all other items to the reference
        foreach (var item in items.Skip(1))
        {
            item.SyncTimestampsFrom(referenceItem);
        }

        // Force UI refresh for all items
        foreach (var item in items)
        {
            item.RefreshTimeInProgress();
            item.RefreshTimeOnDeck();
        }
    }

    /// <summary>
    /// Synchronizes timestamps for a linked group using a specific item as the reference.
    /// </summary>
    /// <param name="items">Items in the linked group to synchronize</param>
    /// <param name="referenceItem">The item to use as the timestamp source</param>
    public void SyncLinkedGroupTimestamps(IReadOnlyList<OrderItem> items, OrderItem referenceItem)
    {
        if (items == null || items.Count == 0 || referenceItem == null)
            return;

        // Sync all items to the reference (excluding the reference itself)
        foreach (var item in items)
        {
            if (item.Id != referenceItem.Id)
            {
                item.SyncTimestampsFrom(referenceItem);
            }
        }

        // Force UI refresh for all items
        foreach (var item in items)
        {
            item.RefreshTimeInProgress();
            item.RefreshTimeOnDeck();
        }
    }
}
