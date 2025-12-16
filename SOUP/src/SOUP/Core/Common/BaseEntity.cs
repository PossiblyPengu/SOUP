using System;

namespace SOUP.Core.Common;

/// <summary>
/// Base class for all domain entities providing common tracking properties.
/// </summary>
/// <remarks>
/// All entities inherit from this class to ensure consistent identity,
/// audit tracking, and soft-delete support across the application.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the UTC timestamp when this entity was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Gets or sets whether this entity has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }
}
