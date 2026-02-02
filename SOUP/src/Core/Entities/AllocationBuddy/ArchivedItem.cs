namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Archived item data.
/// </summary>
public class ArchivedItem
{
    public string ItemNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? SKU { get; set; }
}
