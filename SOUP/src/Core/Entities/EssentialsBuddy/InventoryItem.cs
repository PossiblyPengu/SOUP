using System;
using System.Collections.Generic;
using SOUP.Core.Common;

namespace SOUP.Core.Entities.EssentialsBuddy;

/// <summary>
/// Represents an inventory item with stock level tracking and essential item status.
/// </summary>
/// <remarks>
/// Items are matched against the dictionary database to determine if they are marked
/// as essential. Essential items should always be in stock and are highlighted when
/// low or out of stock.
/// </remarks>
public class InventoryItem : BaseEntity
{
    /// <summary>
    /// Global default for low stock threshold when item doesn't have one set.
    /// </summary>
    public static int GlobalLowStockThreshold { get; set; } = 10;

    /// <summary>
    /// Global default for sufficient stock threshold when item doesn't have one set.
    /// </summary>
    public static int GlobalSufficientThreshold { get; set; } = 100;

    /// <summary>
    /// Gets or sets the item number identifier.
    /// </summary>
    public string ItemNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Universal Product Code (barcode).
    /// </summary>
    public string Upc { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item description from the import source.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description from the dictionary database if matched.
    /// </summary>
    public string? DictionaryDescription { get; set; }

    /// <summary>
    /// Gets or sets whether this item is marked as essential in the dictionary.
    /// </summary>
    public bool IsEssential { get; set; }

    /// <summary>
    /// Gets or sets whether this item is marked as private label in the dictionary.
    /// </summary>
    public bool IsPrivateLabel { get; set; }

    /// <summary>
    /// Gets or sets the tags from the dictionary.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets the tags as a comma-separated display string.
    /// </summary>
    public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : "";

    /// <summary>
    /// Gets or sets whether this item was successfully matched against the dictionary.
    /// </summary>
    public bool DictionaryMatched { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is a non-essential item in a 9-90 bin (misplaced).
    /// </summary>
    public bool IsNonEssentialIn990Bin => !IsEssential && (BinCode?.StartsWith("9-90", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Gets or sets the bin location code.
    /// </summary>
    public string? BinCode { get; set; }

    /// <summary>
    /// Gets or sets the store/warehouse location.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the item category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the current quantity on hand.
    /// </summary>
    public int QuantityOnHand { get; set; }

    /// <summary>
    /// Gets or sets the minimum stock threshold (reorder point).
    /// </summary>
    public int? MinimumThreshold { get; set; }

    /// <summary>
    /// Gets or sets the maximum stock threshold (overstock point).
    /// </summary>
    public int? MaximumThreshold { get; set; }

    /// <summary>
    /// Gets or sets the minimum threshold (convenience accessor).
    /// </summary>
    public int MinThreshold { get => MinimumThreshold ?? 0; set => MinimumThreshold = value; }

    /// <summary>
    /// Gets or sets the maximum threshold (convenience accessor).
    /// </summary>
    public int MaxThreshold { get => MaximumThreshold ?? 0; set => MaximumThreshold = value; }

    /// <summary>
    /// Gets or sets the unit cost.
    /// </summary>
    public decimal? UnitCost { get; set; }

    /// <summary>
    /// Gets or sets the unit selling price.
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime? LastUpdated { get; set; }

    /// <summary>
    /// Gets a value indicating whether quantity is below the minimum threshold.
    /// </summary>
    public bool IsBelowThreshold => QuantityOnHand < (MinimumThreshold ?? 0);

    /// <summary>
    /// Gets the display description, preferring dictionary description if available.
    /// </summary>
    public string DisplayDescription => !string.IsNullOrEmpty(DictionaryDescription)
        ? DictionaryDescription
        : Description;

    /// <summary>
    /// Gets the current inventory status based on quantity and thresholds.
    /// </summary>
    public InventoryStatus Status => CalculateStatus();

    /// <summary>
    /// Provides a numeric sort order for the status to allow custom ordering in UI lists/grids.
    /// Lower numbers sort earlier. Order: OutOfStock (No Stock), Low, InStock, Sufficient.
    /// </summary>
    public int StatusSortOrder => Status switch
    {
        InventoryStatus.OutOfStock => 0,
        InventoryStatus.Low => 1,
        InventoryStatus.InStock => 2,
        InventoryStatus.Sufficient => 3,
        _ => 4
    };

    /// <summary>
    /// Calculates the inventory status based on current quantity and thresholds.
    /// Uses per-item thresholds if set, otherwise falls back to global defaults.
    /// </summary>
    /// <returns>The calculated inventory status.</returns>
    private InventoryStatus CalculateStatus()
    {
        // Use per-item threshold if set, otherwise use global default
        var lowThreshold = MinimumThreshold ?? GlobalLowStockThreshold;
        var sufficientThreshold = MaximumThreshold ?? GlobalSufficientThreshold;

        return QuantityOnHand switch
        {
            0 => InventoryStatus.OutOfStock,
            var q when q < lowThreshold => InventoryStatus.Low,
            var q when q > sufficientThreshold => InventoryStatus.Sufficient,
            _ => InventoryStatus.InStock
        };
    }
}

/// <summary>
/// Represents the stock status of an inventory item.
/// </summary>
public enum InventoryStatus
{
    /// <summary>Item is in stock at normal levels.</summary>
    InStock,

    /// <summary>Item is below the minimum threshold.</summary>
    Low,

    /// <summary>Item has zero quantity.</summary>
    OutOfStock,

    /// <summary>Item is above the maximum threshold (sufficient stock).</summary>
    Sufficient
}
