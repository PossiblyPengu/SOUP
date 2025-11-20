using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents a store allocation entry
/// </summary>
public class AllocationEntry : BaseEntity
{
    public required string ItemNumber { get; set; }
    public required string Description { get; set; }
    public required string StoreId { get; set; }
    public string? StoreName { get; set; }
    public int Quantity { get; set; }
    public StoreRank Rank { get; set; }
    public string? Category { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateTime? AllocationDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Store ranking for allocation priority
/// </summary>
public enum StoreRank
{
    A,  // Highest priority
    B,
    C,
    D   // Lowest priority
}
