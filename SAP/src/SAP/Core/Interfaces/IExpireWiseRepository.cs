using SAP.Core.Entities.ExpireWise;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAP.Core.Interfaces;

public interface IExpireWiseRepository : IRepository<ExpirationItem>
{
    Task<IEnumerable<ExpirationItem>> GetExpiredItemsAsync();
    Task<IEnumerable<ExpirationItem>> GetExpiringSoonAsync(int days = 7);
    Task<IEnumerable<ExpirationItem>> GetByDateRangeAsync(DateTime start, DateTime end);
}
