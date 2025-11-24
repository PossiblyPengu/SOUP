namespace BusinessToolsSuite.Infrastructure.Data;

/// <summary>
/// Unit of Work implementation for LiteDB
/// </summary>
public class LiteDbUnitOfWork : IUnitOfWork
{
    private readonly LiteDbContext _context;
    private bool _disposed;
    private bool _inTransaction;

    public LiteDbUnitOfWork(LiteDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // LiteDB commits automatically unless in a transaction
        // Return number of pending changes (simulated as 1 if successful)
        return Task.FromResult(1);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_inTransaction)
            throw new InvalidOperationException("Transaction already started");

        _context.BeginTrans();
        _inTransaction = true;
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!_inTransaction)
            throw new InvalidOperationException("No active transaction");

        _context.Commit();
        _inTransaction = false;
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!_inTransaction)
            throw new InvalidOperationException("No active transaction");

        _context.Rollback();
        _inTransaction = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_inTransaction)
        {
            _context.Rollback();
            _inTransaction = false;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
