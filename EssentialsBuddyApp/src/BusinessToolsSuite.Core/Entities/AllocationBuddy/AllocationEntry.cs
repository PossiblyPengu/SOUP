using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents a store allocation entry
/// </summary>
public class AllocationEntry : BaseEntity
{
    public string ItemNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string? StoreName { get; set; }
    // Optional SKU value (some dictionaries identify by SKU)
    public string? SKU { get; set; }
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
