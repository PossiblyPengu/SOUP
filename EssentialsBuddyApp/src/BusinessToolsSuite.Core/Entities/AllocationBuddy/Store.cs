using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents a store in the allocation system
/// </summary>
public class Store : BaseEntity
{
    public required string StoreId { get; set; }
    public required string StoreName { get; set; }
    public StoreRank Rank { get; set; }
    public string? Region { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
