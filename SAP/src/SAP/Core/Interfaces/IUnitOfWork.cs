using System;
using System.Threading.Tasks;

namespace SAP.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IAllocationBuddyRepository AllocationBuddy { get; }
    IEssentialsBuddyRepository EssentialsBuddy { get; }
    IExpireWiseRepository ExpireWise { get; }
    Task<int> SaveChangesAsync();
}
