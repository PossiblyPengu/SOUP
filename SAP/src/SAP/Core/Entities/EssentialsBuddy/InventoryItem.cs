using SAP.Core.Common;
using System;

namespace SAP.Core.Entities.EssentialsBuddy;

/// <summary>
/// Represents an inventory item
/// </summary>
public class InventoryItem : BaseEntity
{
    public string ItemNumber { get; set; } = string.Empty;
    public string Upc { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Description from the dictionary database (if matched)
    /// </summary>
    public string? DictionaryDescription { get; set; }
    
    /// <summary>
    /// Whether this item is marked as essential in the dictionary
    /// </summary>
    public bool IsEssential { get; set; }
    
    /// <summary>
    /// Whether this item was matched against the dictionary
    /// </summary>
    public bool DictionaryMatched { get; set; }
    
    /// <summary>
    /// Whether this is a non-essential item in a 9-90 bin (shouldn't be there)
    /// </summary>
    public bool IsNonEssentialIn990Bin => !IsEssential && (BinCode?.StartsWith("9-90", StringComparison.OrdinalIgnoreCase) ?? false);
    
    public string? BinCode { get; set; }
    public string? Location { get; set; }
    public string? Category { get; set; }
    public int QuantityOnHand { get; set; }
    public int? MinimumThreshold { get; set; }
    public int? MaximumThreshold { get; set; }
    public int MinThreshold { get => MinimumThreshold ?? 0; set => MinimumThreshold = value; }
    public int MaxThreshold { get => MaximumThreshold ?? 0; set => MaximumThreshold = value; }
    public decimal? UnitCost { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateTime? LastUpdated { get; set; }
    public bool IsBelowThreshold => QuantityOnHand < (MinimumThreshold ?? 0);
    
    /// <summary>
    /// Display description - prefers dictionary description if available
    /// </summary>
    public string DisplayDescription => !string.IsNullOrEmpty(DictionaryDescription) 
        ? DictionaryDescription 
        : Description;
    
    public InventoryStatus Status => CalculateStatus();

    private InventoryStatus CalculateStatus()
    {
        if (MinimumThreshold is null)
            return InventoryStatus.InStock;

        return QuantityOnHand switch
        {
            0 => InventoryStatus.OutOfStock,
            var q when q < MinimumThreshold => InventoryStatus.Low,
            var q when MaximumThreshold.HasValue && q > MaximumThreshold => InventoryStatus.Overstocked,
            _ => InventoryStatus.InStock
        };
    }
}

public enum InventoryStatus
{
    InStock,
    Low,
    OutOfStock,
    Overstocked
}
