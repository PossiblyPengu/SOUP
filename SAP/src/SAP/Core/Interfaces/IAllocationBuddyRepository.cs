using SAP.Core.Entities.AllocationBuddy;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAP.Core.Interfaces;

public interface IAllocationBuddyRepository : IRepository<AllocationEntry>
{
    Task<IEnumerable<AllocationEntry>> GetByArchiveIdAsync(Guid archiveId);
    Task<IEnumerable<AllocationEntry>> GetActiveEntriesAsync();
    Task<IEnumerable<AllocationArchive>> GetAllArchivesAsync();
    Task<AllocationArchive> CreateArchiveAsync(string name, string? notes = null);
    Task ArchiveEntriesAsync(Guid archiveId, IEnumerable<Guid> entryIds);
}
