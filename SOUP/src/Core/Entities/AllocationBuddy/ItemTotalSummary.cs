namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Summary of total quantity for a unique item across all locations.
/// </summary>
public class ItemTotalSummary
{
    public string ItemNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int LocationCount { get; set; }

    /// <summary>Quantity remaining in the item pool (unallocated).</summary>
    public int PoolQuantity { get; set; }

    /// <summary>Total allocated quantity (TotalQuantity - PoolQuantity).</summary>
    public int AllocatedQuantity => TotalQuantity - PoolQuantity;

    /// <summary>Whether this item has any remaining in the pool.</summary>
    public bool HasPoolItems => PoolQuantity > 0;
}
