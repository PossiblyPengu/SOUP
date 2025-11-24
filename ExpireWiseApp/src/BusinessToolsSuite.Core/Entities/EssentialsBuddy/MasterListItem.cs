using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Entities.EssentialsBuddy;

/// <summary>
/// Represents a master list item with pre-configured thresholds
/// </summary>
public class MasterListItem : BaseEntity
{
    public required string ItemNumber { get; set; }
    public required string Description { get; set; }
    public int DefaultMinimumThreshold { get; set; }
    public int DefaultMaximumThreshold { get; set; }
    public string? Category { get; set; }
    public bool IsEssential { get; set; } = true;
}
