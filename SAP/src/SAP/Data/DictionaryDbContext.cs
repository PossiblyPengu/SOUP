using System;
using System.IO;
using LiteDB;
using SAP.Data.Entities;

namespace SAP.Data;

/// <summary>
/// Shared LiteDB context for dictionary data (items and stores).
/// Uses a separate database from the main app data for clean separation.
/// </summary>
public sealed class DictionaryDbContext : IDisposable
{
    private static readonly Lazy<DictionaryDbContext> _instance = new(() => new DictionaryDbContext());
    private readonly LiteDatabase _database;
    private bool _disposed;

    /// <summary>
    /// Singleton instance for shared dictionary access
    /// </summary>
    public static DictionaryDbContext Instance => _instance.Value;

    /// <summary>
    /// Path to the shared dictionary database
    /// </summary>
    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SAP",
        "Shared",
        "dictionaries.db"
    );

    private DictionaryDbContext()
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _database = new LiteDatabase(DatabasePath);
        
        // Ensure indexes for fast lookups
        var items = _database.GetCollection<DictionaryItemEntity>("items");
        items.EnsureIndex(x => x.Description);
        items.EnsureIndex(x => x.Skus);
        
        var stores = _database.GetCollection<StoreEntity>("stores");
        stores.EnsureIndex(x => x.Name);
        stores.EnsureIndex(x => x.Rank);
    }

    /// <summary>
    /// Get the items collection
    /// </summary>
    public ILiteCollection<DictionaryItemEntity> Items => _database.GetCollection<DictionaryItemEntity>("items");

    /// <summary>
    /// Get the stores collection
    /// </summary>
    public ILiteCollection<StoreEntity> Stores => _database.GetCollection<StoreEntity>("stores");

    /// <summary>
    /// Get the underlying database for advanced operations
    /// </summary>
    public LiteDatabase Database => _database;

    /// <summary>
    /// Check if the database has been initialized with data
    /// </summary>
    public bool HasItems => Items.Count() > 0;
    
    /// <summary>
    /// Check if stores have been initialized
    /// </summary>
    public bool HasStores => Stores.Count() > 0;

    public void Dispose()
    {
        if (!_disposed)
        {
            _database?.Dispose();
            _disposed = true;
        }
    }
}
