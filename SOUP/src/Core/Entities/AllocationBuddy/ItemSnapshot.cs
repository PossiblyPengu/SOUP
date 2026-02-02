namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Snapshot of an item's state for undo operations.
/// </summary>
public class ItemSnapshot
{
    public string ItemNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public string? SKU { get; set; }
}
