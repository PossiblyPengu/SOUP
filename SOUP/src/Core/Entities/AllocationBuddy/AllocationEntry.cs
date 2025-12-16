using SOUP.Core.Common;
using System;

namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents a single allocation entry that assigns an item to a store location.
/// </summary>
/// <remarks>
/// This is the core data structure parsed from Excel/CSV imports and used
/// to build the hierarchical location-based allocation view.
/// </remarks>
public class AllocationEntry : BaseEntity
{
    /// <summary>
    /// Gets or sets the item number/SKU identifier.
    /// </summary>
    public string ItemNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the item description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the store/location identifier.
    /// </summary>
    public string StoreId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the store/location name.
    /// </summary>
    public string? StoreName { get; set; }
    
    /// <summary>
    /// Gets or sets the SKU (Stock Keeping Unit) if different from item number.
    /// </summary>
    public string? SKU { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity allocated.
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Gets or sets the store's priority rank for allocation.
    /// </summary>
    public StoreRank Rank { get; set; }
    
    /// <summary>
    /// Gets or sets the item category.
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Gets or sets the unit price of the item.
    /// </summary>
    public decimal? UnitPrice { get; set; }
    
    /// <summary>
    /// Gets or sets the date of the allocation.
    /// </summary>
    public DateTime? AllocationDate { get; set; }
    
    /// <summary>
    /// Gets or sets any additional notes.
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the archive this entry belongs to, if any.
    /// </summary>
    public Guid? ArchiveId { get; set; }
}

/// <summary>
/// Store ranking for allocation priority, determining order of distribution.
/// </summary>
public enum StoreRank
{
    /// <summary>Highest priority stores.</summary>
    A,
    /// <summary>Second priority stores.</summary>
    B,
    /// <summary>Third priority stores.</summary>
    C,
    /// <summary>Lowest priority stores.</summary>
    D
}
