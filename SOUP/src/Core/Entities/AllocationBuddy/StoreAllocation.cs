namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents a store's allocation for a specific item (used in item-sorted view).
/// </summary>
public class StoreAllocation
{
    public string StoreCode { get; set; } = string.Empty;
    public string? StoreName { get; set; }
    public int Quantity { get; set; }
}
