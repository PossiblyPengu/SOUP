using System;
using LiteDB;

namespace SOUP.Infrastructure.Data;

/// <summary>
/// LiteDB database context wrapper for managing database connections and collections
/// </summary>
public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private bool _disposed;

    public LiteDbContext(string databasePath)
    {
        // Build optimized connection string
        // Direct mode for single-process desktop app, InitialSize reduces fragmentation
        var connectionString = $"Filename={databasePath};Connection=Direct;InitialSize=1MB";
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
        if (!_disposed)
        {
            _database?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
