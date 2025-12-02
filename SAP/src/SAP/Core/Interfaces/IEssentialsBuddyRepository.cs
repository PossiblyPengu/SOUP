using SAP.Core.Entities.EssentialsBuddy;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAP.Core.Interfaces;

public interface IEssentialsBuddyRepository : IRepository<InventoryItem>
{
    Task<IEnumerable<InventoryItem>> GetItemsBelowThresholdAsync();
    Task<IEnumerable<MasterListItem>> GetMasterListAsync();
    Task UpdateMasterListAsync(IEnumerable<MasterListItem> items);
}
