using BusinessToolsSuite.Core.Common;

namespace BusinessToolsSuite.Core.Entities.EssentialsBuddy;

/// <summary>
/// Represents an inventory item from Business Central
/// </summary>
public class InventoryItem : BaseEntity
{
    public required string ItemNumber { get; set; }
    public required string Description { get; set; }
    public string? BinCode { get; set; }
    public int QuantityOnHand { get; set; }
    public int? MinimumThreshold { get; set; }
    public int? MaximumThreshold { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Category { get; set; }
    public InventoryStatus Status => CalculateStatus();
    public DateTime? LastUpdated { get; set; }

    private InventoryStatus CalculateStatus()
    {
        if (MinimumThreshold is null)
            return InventoryStatus.Normal;

        return QuantityOnHand switch
        {
            0 => InventoryStatus.OutOfStock,
            var q when q < MinimumThreshold => InventoryStatus.Low,
            var q when MaximumThreshold.HasValue && q > MaximumThreshold => InventoryStatus.Overstocked,
            _ => InventoryStatus.Normal
        };
    }
}

/// <summary>
/// Inventory status enumeration
/// </summary>
public enum InventoryStatus
{
    Normal,
    Low,
    OutOfStock,
    Overstocked
}
