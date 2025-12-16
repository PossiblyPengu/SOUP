using System;
using System.Threading.Tasks;
using SOUP.Core.Interfaces;

namespace SOUP.Infrastructure.Data;

/// <summary>
/// Unit of Work implementation for LiteDB
/// </summary>
public class LiteDbUnitOfWork : IUnitOfWork
{
    private readonly LiteDbContext _context;
    private bool _disposed;

    public IAllocationBuddyRepository AllocationBuddy { get; }
    public IEssentialsBuddyRepository EssentialsBuddy { get; }
    public IExpireWiseRepository ExpireWise { get; }

    public LiteDbUnitOfWork(
        LiteDbContext context,
        IAllocationBuddyRepository allocationBuddyRepository,
        IEssentialsBuddyRepository essentialsBuddyRepository,
        IExpireWiseRepository expireWiseRepository)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        AllocationBuddy = allocationBuddyRepository ?? throw new ArgumentNullException(nameof(allocationBuddyRepository));
        EssentialsBuddy = essentialsBuddyRepository ?? throw new ArgumentNullException(nameof(essentialsBuddyRepository));
        ExpireWise = expireWiseRepository ?? throw new ArgumentNullException(nameof(expireWiseRepository));
    }

    public Task<int> SaveChangesAsync()
    {
        // LiteDB auto-saves on each operation, so this is a no-op
        // Return 1 to indicate success
        return Task.FromResult(1);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Context is managed by DI container, don't dispose here
            }
            _disposed = true;
        }
    }
}
