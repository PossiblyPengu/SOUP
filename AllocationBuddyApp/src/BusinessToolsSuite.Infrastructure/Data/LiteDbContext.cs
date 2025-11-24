namespace BusinessToolsSuite.Infrastructure.Data;

/// <summary>
/// LiteDB database context for managing database connections
/// </summary>
public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private bool _disposed;

    public LiteDbContext(string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        _database = new LiteDatabase(connectionString);
    }

    public ILiteCollection<T> GetCollection<T>(string? collectionName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _database.GetCollection<T>(collectionName);
    }

    public bool BeginTrans() => _database.BeginTrans();
    public bool Commit() => _database.Commit();
    public bool Rollback() => _database.Rollback();

    public void Dispose()
    {
        if (_disposed)
            return;

        _database?.Dispose();
        _disposed = true;
    }
}
