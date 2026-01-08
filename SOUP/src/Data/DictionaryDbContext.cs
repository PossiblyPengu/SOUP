using System;
using System.IO;
using LiteDB;
using SOUP.Data.Entities;

namespace SOUP.Data;

/// <summary>
/// Shared LiteDB context for dictionary data (items and stores).
/// Uses a separate database from the main app data for clean separation.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class DictionaryDbContext : IDisposable
{
    private static readonly Lazy<DictionaryDbContext> _instance = new(() => new DictionaryDbContext());
    private static readonly object _lock = new();
    private readonly LiteDatabase _database;
    private bool _disposed;

    /// <summary>
    /// Singleton instance for shared dictionary access
    /// </summary>
    public static DictionaryDbContext Instance => _instance.Value;

    /// <summary>
    /// Path to the shared dictionary database
    /// </summary>
    public static string DatabasePath => Core.AppPaths.DictionaryDbPath;

    private DictionaryDbContext()
    {
        Directory.CreateDirectory(Core.AppPaths.SharedDir);

        // Shared connection mode supports multi-threaded access across STA threads (widget, main window)
        var connectionString = $"Filename={DatabasePath};Connection=Shared;InitialSize=2MB";
        _database = new LiteDatabase(connectionString);
        
        // Ensure indexes for fast lookups
        var items = _database.GetCollection<DictionaryItemEntity>("items");
        items.EnsureIndex(x => x.Description);
        items.EnsureIndex(x => x.Skus);
        
        var stores = _database.GetCollection<StoreEntity>("stores");
        stores.EnsureIndex(x => x.Name);
        stores.EnsureIndex(x => x.Rank);
    }

    /// <summary>
    /// Get the items collection (thread-safe)
    /// </summary>
    public ILiteCollection<DictionaryItemEntity> Items
    {
        get
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                return _database.GetCollection<DictionaryItemEntity>("items");
            }
        }
    }

    /// <summary>
    /// Get the stores collection (thread-safe)
    /// </summary>
    public ILiteCollection<StoreEntity> Stores
    {
        get
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                return _database.GetCollection<StoreEntity>("stores");
            }
        }
    }

    /// <summary>
    /// Get the underlying database for advanced operations
    /// </summary>
    public LiteDatabase Database
    {
        get
        {
            ThrowIfDisposed();
            return _database;
        }
    }

    /// <summary>
    /// Check if the database has been initialized with data
    /// </summary>
    public bool HasItems
    {
        get
        {
            ThrowIfDisposed();
            return Items.Exists(_ => true);
        }
    }
    
    /// <summary>
    /// Check if stores have been initialized
    /// </summary>
    public bool HasStores
    {
        get
        {
            ThrowIfDisposed();
            return Stores.Exists(_ => true);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DictionaryDbContext));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _database?.Dispose();
                Serilog.Log.Debug("DictionaryDbContext disposed successfully");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error disposing DictionaryDbContext");
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Check if the context has been disposed
    /// </summary>
    public bool IsDisposed => _disposed;
}
