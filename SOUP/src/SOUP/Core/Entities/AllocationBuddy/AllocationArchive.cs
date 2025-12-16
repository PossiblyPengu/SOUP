using SOUP.Core.Common;
using System;

namespace SOUP.Core.Entities.AllocationBuddy;

/// <summary>
/// Represents an archived allocation session with metadata.
/// </summary>
/// <remarks>
/// Archives are created automatically when the previous session data is replaced
/// with new import data, or manually by the user. The archive contains metadata
/// about the archived entries while the actual entry data is stored separately.
/// </remarks>
public class AllocationArchive : BaseEntity
{
    /// <summary>
    /// Gets or sets the archive name (typically auto-generated with timestamp).
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when this archive was created.
    /// </summary>
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the number of entries in this archive.
    /// </summary>
    public int EntryCount { get; set; }
    
    /// <summary>
    /// Gets or sets optional notes about this archive.
    /// </summary>
    public string? Notes { get; set; }
}
