using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SOUP.Data.Entities;

namespace SOUP.Data;

/// <summary>
/// Shared SQLite context for dictionary data (items and stores).
/// Uses a separate database from the main app data for clean separation.
/// Thread-safe for concurrent access using WAL mode.
/// </summary>
public sealed class DictionaryDbContext : IDisposable
{
    private static readonly Lazy<DictionaryDbContext> _instance = new(() => new DictionaryDbContext());
    private static readonly Lock _lock = new();
    private readonly string _connectionString;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

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

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();

        // Enable WAL mode for multi-process support
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        // Create items table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Items (
                    Number TEXT PRIMARY KEY,
                    Description TEXT NOT NULL DEFAULT '',
                    Skus TEXT NOT NULL DEFAULT '[]',
                    IsEssential INTEGER NOT NULL DEFAULT 0,
                    IsPrivateLabel INTEGER NOT NULL DEFAULT 0,
                    Tags TEXT NOT NULL DEFAULT '[]'
                );
                CREATE INDEX IF NOT EXISTS IX_Items_Description ON Items(Description);
            ";
            cmd.ExecuteNonQuery();
        }

        // Create stores table
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Stores (
                    Code TEXT PRIMARY KEY,
                    Name TEXT NOT NULL DEFAULT '',
                    Rank TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS IX_Stores_Name ON Stores(Name);
                CREATE INDEX IF NOT EXISTS IX_Stores_Rank ON Stores(Rank);
            ";
            cmd.ExecuteNonQuery();
        }

        Serilog.Log.Debug("DictionaryDbContext SQLite initialized at {Path}", DatabasePath);
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    #region Items Operations

    /// <summary>
    /// Get all dictionary items
    /// </summary>
    public List<DictionaryItemEntity> GetAllItems()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Number, Description, Skus, IsEssential, IsPrivateLabel, Tags FROM Items";

            List<DictionaryItemEntity> items = new();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new DictionaryItemEntity
                {
                    Number = reader.GetString(0),
                    Description = reader.GetString(1),
                    Skus = JsonSerializer.Deserialize<List<string>>(reader.GetString(2), JsonOptions) ?? new(),
                    IsEssential = reader.GetInt32(3) == 1,
                    IsPrivateLabel = reader.GetInt32(4) == 1,
                    Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(5), JsonOptions) ?? new()
                });
            }
            return items;
        }
    }

    /// <summary>
    /// Get a dictionary item by number
    /// </summary>
    public DictionaryItemEntity? GetItem(string number)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Number, Description, Skus, IsEssential, IsPrivateLabel, Tags FROM Items WHERE Number = @Number";
            cmd.Parameters.AddWithValue("@Number", number);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new DictionaryItemEntity
                {
                    Number = reader.GetString(0),
                    Description = reader.GetString(1),
                    Skus = JsonSerializer.Deserialize<List<string>>(reader.GetString(2), JsonOptions) ?? new(),
                    IsEssential = reader.GetInt32(3) == 1,
                    IsPrivateLabel = reader.GetInt32(4) == 1,
                    Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(5), JsonOptions) ?? new()
                };
            }
            return null;
        }
    }

    /// <summary>
    /// Find items matching a predicate (loads all and filters in memory)
    /// </summary>
    public List<DictionaryItemEntity> FindItems(Func<DictionaryItemEntity, bool> predicate, int? maxResults = null)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Number, Description, Skus, IsEssential, IsPrivateLabel, Tags FROM Items";

            List<DictionaryItemEntity> results = new();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var item = new DictionaryItemEntity
                {
                    Number = reader.GetString(0),
                    Description = reader.GetString(1),
                    Skus = JsonSerializer.Deserialize<List<string>>(reader.GetString(2), JsonOptions) ?? new(),
                    IsEssential = reader.GetInt32(3) == 1,
                    IsPrivateLabel = reader.GetInt32(4) == 1,
                    Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(5), JsonOptions) ?? new()
                };

                if (predicate(item))
                {
                    results.Add(item);
                    if (maxResults.HasValue && results.Count >= maxResults.Value)
                        break;
                }
            }
            return results;
        }
    }

    /// <summary>
    /// Get the count of items
    /// </summary>
    public int GetItemCount()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Items";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    /// <summary>
    /// Insert or update a dictionary item
    /// </summary>
    public void UpsertItem(DictionaryItemEntity item)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Items (Number, Description, Skus, IsEssential, IsPrivateLabel, Tags)
                VALUES (@Number, @Description, @Skus, @IsEssential, @IsPrivateLabel, @Tags)
                ON CONFLICT(Number) DO UPDATE SET
                    Description = excluded.Description,
                    Skus = excluded.Skus,
                    IsEssential = excluded.IsEssential,
                    IsPrivateLabel = excluded.IsPrivateLabel,
                    Tags = excluded.Tags
            ";
            cmd.Parameters.AddWithValue("@Number", item.Number);
            cmd.Parameters.AddWithValue("@Description", item.Description);
            cmd.Parameters.AddWithValue("@Skus", JsonSerializer.Serialize(item.Skus, JsonOptions));
            cmd.Parameters.AddWithValue("@IsEssential", item.IsEssential ? 1 : 0);
            cmd.Parameters.AddWithValue("@IsPrivateLabel", item.IsPrivateLabel ? 1 : 0);
            cmd.Parameters.AddWithValue("@Tags", JsonSerializer.Serialize(item.Tags, JsonOptions));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Insert or update multiple dictionary items in a transaction
    /// </summary>
    public void UpsertItems(IEnumerable<DictionaryItemEntity> items)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var item in items)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Items (Number, Description, Skus, IsEssential, IsPrivateLabel, Tags)
                        VALUES (@Number, @Description, @Skus, @IsEssential, @IsPrivateLabel, @Tags)
                        ON CONFLICT(Number) DO UPDATE SET
                            Description = excluded.Description,
                            Skus = excluded.Skus,
                            IsEssential = excluded.IsEssential,
                            IsPrivateLabel = excluded.IsPrivateLabel,
                            Tags = excluded.Tags
                    ";
                    cmd.Parameters.AddWithValue("@Number", item.Number);
                    cmd.Parameters.AddWithValue("@Description", item.Description);
                    cmd.Parameters.AddWithValue("@Skus", JsonSerializer.Serialize(item.Skus, JsonOptions));
                    cmd.Parameters.AddWithValue("@IsEssential", item.IsEssential ? 1 : 0);
                    cmd.Parameters.AddWithValue("@IsPrivateLabel", item.IsPrivateLabel ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Tags", JsonSerializer.Serialize(item.Tags, JsonOptions));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    /// <summary>
    /// Delete a dictionary item
    /// </summary>
    public bool DeleteItem(string number)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Items WHERE Number = @Number";
            cmd.Parameters.AddWithValue("@Number", number);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    /// <summary>
    /// Delete all dictionary items
    /// </summary>
    public int DeleteAllItems()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Items";
            return cmd.ExecuteNonQuery();
        }
    }

    #endregion

    #region Stores Operations

    /// <summary>
    /// Get all stores
    /// </summary>
    public List<StoreEntity> GetAllStores()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Code, Name, Rank FROM Stores";

            List<StoreEntity> stores = new();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                stores.Add(new StoreEntity
                {
                    Code = reader.GetString(0),
                    Name = reader.GetString(1),
                    Rank = reader.GetString(2)
                });
            }
            return stores;
        }
    }

    /// <summary>
    /// Get a store by code
    /// </summary>
    public StoreEntity? GetStore(string code)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Code, Name, Rank FROM Stores WHERE Code = @Code";
            cmd.Parameters.AddWithValue("@Code", code);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new StoreEntity
                {
                    Code = reader.GetString(0),
                    Name = reader.GetString(1),
                    Rank = reader.GetString(2)
                };
            }
            return null;
        }
    }

    /// <summary>
    /// Find stores matching a predicate
    /// </summary>
    public List<StoreEntity> FindStores(Func<StoreEntity, bool> predicate, int? maxResults = null)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Code, Name, Rank FROM Stores";

            List<StoreEntity> results = new();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var store = new StoreEntity
                {
                    Code = reader.GetString(0),
                    Name = reader.GetString(1),
                    Rank = reader.GetString(2)
                };

                if (predicate(store))
                {
                    results.Add(store);
                    if (maxResults.HasValue && results.Count >= maxResults.Value)
                        break;
                }
            }
            return results;
        }
    }

    /// <summary>
    /// Get the count of stores
    /// </summary>
    public int GetStoreCount()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Stores";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    /// <summary>
    /// Insert or update a store
    /// </summary>
    public void UpsertStore(StoreEntity store)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Stores (Code, Name, Rank)
                VALUES (@Code, @Name, @Rank)
                ON CONFLICT(Code) DO UPDATE SET
                    Name = excluded.Name,
                    Rank = excluded.Rank
            ";
            cmd.Parameters.AddWithValue("@Code", store.Code);
            cmd.Parameters.AddWithValue("@Name", store.Name);
            cmd.Parameters.AddWithValue("@Rank", store.Rank);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Insert or update multiple stores in a transaction
    /// </summary>
    public void UpsertStores(IEnumerable<StoreEntity> stores)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var store in stores)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Stores (Code, Name, Rank)
                        VALUES (@Code, @Name, @Rank)
                        ON CONFLICT(Code) DO UPDATE SET
                            Name = excluded.Name,
                            Rank = excluded.Rank
                    ";
                    cmd.Parameters.AddWithValue("@Code", store.Code);
                    cmd.Parameters.AddWithValue("@Name", store.Name);
                    cmd.Parameters.AddWithValue("@Rank", store.Rank);
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    /// <summary>
    /// Delete a store
    /// </summary>
    public bool DeleteStore(string code)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Stores WHERE Code = @Code";
            cmd.Parameters.AddWithValue("@Code", code);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    /// <summary>
    /// Delete all stores
    /// </summary>
    public int DeleteAllStores()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Stores";
            return cmd.ExecuteNonQuery();
        }
    }

    #endregion

    /// <summary>
    /// Check if the database has been initialized with data
    /// </summary>
    public bool HasItems
    {
        get
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                using var connection = CreateConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM Items LIMIT 1)";
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
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
            lock (_lock)
            {
                using var connection = CreateConnection();
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM Stores LIMIT 1)";
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
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
            _disposed = true;
            Serilog.Log.Debug("DictionaryDbContext disposed successfully");
        }
    }

    /// <summary>
    /// Check if the context has been disposed
    /// </summary>
    public bool IsDisposed => _disposed;
}
