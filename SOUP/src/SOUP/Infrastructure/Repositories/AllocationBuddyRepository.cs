using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using SOUP.Core.Common;
using SOUP.Core.Entities.AllocationBuddy;
using SOUP.Core.Interfaces;
using SOUP.Infrastructure.Data;

namespace SOUP.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Allocation Buddy entries
/// </summary>
public class AllocationBuddyRepository : LiteDbRepository<AllocationEntry>, IAllocationBuddyRepository
{
    private readonly ILiteCollection<AllocationArchive> _archiveCollection;

    public AllocationBuddyRepository(LiteDbContext context, ILogger<LiteDbRepository<AllocationEntry>>? logger = null)
        : base(context, logger)
    {
        _archiveCollection = context.GetCollection<AllocationArchive>();
    }

    public async Task<IEnumerable<AllocationEntry>> GetByArchiveIdAsync(Guid archiveId)
    {
        return await FindAsync(e => e.ArchiveId == archiveId && !e.IsDeleted);
    }

    public async Task<IEnumerable<AllocationEntry>> GetActiveEntriesAsync()
    {
        return await FindAsync(e => e.ArchiveId == null && !e.IsDeleted);
    }

    public Task<IEnumerable<AllocationArchive>> GetAllArchivesAsync()
    {
        var archives = _archiveCollection.Query().Where(a => !a.IsDeleted).ToList();
        return Task.FromResult<IEnumerable<AllocationArchive>>(archives);
    }

    public Task<AllocationArchive> CreateArchiveAsync(string name, string? notes = null)
    {
        var archive = new AllocationArchive
        {
            Name = name,
            Notes = notes,
            ArchivedAt = DateTime.UtcNow
        };
        _archiveCollection.Insert(archive);
        return Task.FromResult(archive);
    }

    public async Task ArchiveEntriesAsync(Guid archiveId, IEnumerable<Guid> entryIds)
    {
        var entryIdSet = entryIds.ToHashSet();
        var entries = (await GetAllAsync()).Where(e => entryIdSet.Contains(e.Id)).ToList();
        
        foreach (var entry in entries)
        {
            entry.ArchiveId = archiveId;
            entry.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(entry);
        }

        // Update archive entry count
        var archive = _archiveCollection.FindById(archiveId);
        if (archive != null)
        {
            archive.EntryCount = entries.Count;
            _archiveCollection.Update(archive);
        }
    }
}
