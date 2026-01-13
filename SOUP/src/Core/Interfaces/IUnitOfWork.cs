using System;
using System.Threading.Tasks;

namespace SOUP.Core.Interfaces;

/// <summary>
/// Unit of Work pattern interface for coordinating repository operations.
/// </summary>
/// <remarks>
/// Provides access to all module repositories and manages transaction-like
/// behavior across multiple operations.
/// </remarks>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Gets the AllocationBuddy repository.
    /// </summary>
    IAllocationBuddyRepository AllocationBuddy { get; }

    /// <summary>
    /// Gets the EssentialsBuddy repository.
    /// </summary>
    IEssentialsBuddyRepository EssentialsBuddy { get; }

    /// <summary>
    /// Gets the ExpireWise repository.
    /// </summary>
    IExpireWiseRepository ExpireWise { get; }

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    /// <returns>The number of entities affected.</returns>
    Task<int> SaveChangesAsync();
}
