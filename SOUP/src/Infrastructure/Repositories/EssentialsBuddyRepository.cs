using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.EssentialsBuddy;
using SOUP.Core.Interfaces;
using SOUP.Infrastructure.Data;

namespace SOUP.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Essentials Buddy inventory items
/// </summary>
public class EssentialsBuddyRepository : LiteDbRepository<InventoryItem>, IEssentialsBuddyRepository
{
    private readonly ILiteCollection<MasterListItem> _masterListCollection;

    public EssentialsBuddyRepository(LiteDbContext context, ILogger<LiteDbRepository<InventoryItem>>? logger = null)
        : base(context, logger)
    {
        _masterListCollection = context.GetCollection<MasterListItem>();
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsBelowThresholdAsync()
    {
        var allItems = await GetAllAsync();
        return allItems.Where(i => i.IsBelowThreshold);
    }

    public Task<IEnumerable<MasterListItem>> GetMasterListAsync()
    {
        var items = _masterListCollection.Query().Where(i => !i.IsDeleted).ToList();
        return Task.FromResult<IEnumerable<MasterListItem>>(items);
    }

    public Task UpdateMasterListAsync(IEnumerable<MasterListItem> items)
    {
        foreach (var item in items)
        {
            var existing = _masterListCollection.FindById(item.Id);
            if (existing != null)
            {
                _masterListCollection.Update(item);
            }
            else
            {
                _masterListCollection.Insert(item);
            }
        }
        return Task.CompletedTask;
    }
}
