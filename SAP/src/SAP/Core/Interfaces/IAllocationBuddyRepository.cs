using SAP.Core.Entities.AllocationBuddy;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAP.Core.Interfaces;

/// <summary>
/// Repository interface for AllocationBuddy-specific data operations.
/// </summary>
public interface IAllocationBuddyRepository : IRepository<AllocationEntry>
{
    /// <summary>
    /// Gets all entries belonging to a specific archive.
    /// </summary>
    /// <param name="archiveId">The archive's unique identifier.</param>
    /// <returns>A collection of archived entries.</returns>
    Task<IEnumerable<AllocationEntry>> GetByArchiveIdAsync(Guid archiveId);
    
    /// <summary>
    /// Gets all active (non-archived) entries.
    /// </summary>
    /// <returns>A collection of active entries.</returns>
    Task<IEnumerable<AllocationEntry>> GetActiveEntriesAsync();
    
    /// <summary>
    /// Gets all archives.
    /// </summary>
    /// <returns>A collection of all archives.</returns>
    Task<IEnumerable<AllocationArchive>> GetAllArchivesAsync();
    
    /// <summary>
    /// Creates a new archive.
    /// </summary>
    /// <param name="name">The archive name.</param>
    /// <param name="notes">Optional notes about the archive.</param>
    /// <returns>The created archive.</returns>
    Task<AllocationArchive> CreateArchiveAsync(string name, string? notes = null);
    
    /// <summary>
    /// Moves entries to an archive.
    /// </summary>
    /// <param name="archiveId">The archive's unique identifier.</param>
    /// <param name="entryIds">The IDs of entries to archive.</param>
    Task ArchiveEntriesAsync(Guid archiveId, IEnumerable<Guid> entryIds);
}
