using SAP.Core.Common;

namespace SAP.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents a store/warehouse location for allocation tracking.
/// </summary>
/// <remarks>
/// Stores are extracted from imported allocation data and used for
/// filtering and organizing entries by location.
/// </remarks>
public class Store : BaseEntity
{
    /// <summary>
    /// Gets or sets the store number identifier.
    /// </summary>
    public required string Number { get; set; }
    
    /// <summary>
    /// Gets or sets the store name/description.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Gets or sets whether this store is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
