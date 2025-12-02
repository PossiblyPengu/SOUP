using System;
using LiteDB;

namespace SAP.Infrastructure.Data;

/// <summary>
/// LiteDB database context wrapper for managing database connections and collections
/// </summary>
public class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private bool _disposed;

    public LiteDbContext(string connectionString)
    {
        _database = new LiteDatabase(connectionString);
    }

    /// <summary>
    /// Get a typed collection from the database
    /// </summary>
    public ILiteCollection<T> GetCollection<T>(string? name = null)
    {
        return _database.GetCollection<T>(name ?? typeof(T).Name);
    }

    /// <summary>
    /// Get the underlying LiteDatabase instance
    /// </summary>
    public LiteDatabase Database => _database;

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
                _database?.Dispose();
            }
            _disposed = true;
        }
    }
}
