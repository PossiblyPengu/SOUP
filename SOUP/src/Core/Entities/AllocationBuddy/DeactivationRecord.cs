namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Record of a deactivation operation for undo functionality.
/// </summary>
public class DeactivationRecord
{
    public LocationAllocation? Location { get; set; }
    public List<ItemSnapshot> Items { get; set; } = new();
}
