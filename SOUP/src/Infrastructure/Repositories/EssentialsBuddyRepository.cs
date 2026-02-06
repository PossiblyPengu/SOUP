using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.EssentialsBuddy;
using SOUP.Core.Interfaces;
using SOUP.Infrastructure.Data;

namespace SOUP.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Essentials Buddy inventory items
/// </summary>
public class EssentialsBuddyRepository : SqliteRepository<InventoryItem>, IEssentialsBuddyRepository
{
    public EssentialsBuddyRepository(SqliteDbContext context, ILogger<SqliteRepository<InventoryItem>>? logger = null)
        : base(context, logger)
    {
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsBelowThresholdAsync()
    {
        var allItems = await GetAllAsync();
        return allItems.Where(i => i.IsBelowThreshold);
    }
}

