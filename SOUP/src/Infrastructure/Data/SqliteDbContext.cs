using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SOUP.Core.Common;

namespace SOUP.Infrastructure.Data;

/// <summary>
/// SQLite database context wrapper for managing database connections.
/// Uses WAL mode for better multi-process concurrent access.
/// </summary>
public sealed class SqliteDbContext : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDbContext>? _logger;
    private bool _disposed;

    public string DatabasePath { get; }

    public SqliteDbContext(string databasePath, ILogger<SqliteDbContext>? logger = null)
    {
        DatabasePath = databasePath;
        _logger = logger;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        // Initialize database with WAL mode for multi-process support
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();

            // Enable WAL mode for better concurrent access from multiple processes
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }

            // Enable foreign keys
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys=ON;";
                cmd.ExecuteNonQuery();
            }

            _logger?.LogDebug("SQLite database initialized at {Path} with WAL mode", DatabasePath);
        }
        catch (SqliteException ex)
        {
            _logger?.LogError(ex, "Failed to initialize SQLite database at {Path}. Error code: {ErrorCode}", DatabasePath, ex.SqliteErrorCode);
            throw new InvalidOperationException($"Failed to initialize database at {DatabasePath}. The database file may be corrupted or locked by another process.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.LogError(ex, "Access denied when initializing SQLite database at {Path}", DatabasePath);
            throw new InvalidOperationException($"Access denied to database at {DatabasePath}. Check file permissions.", ex);
        }
        catch (IOException ex)
        {
            _logger?.LogError(ex, "I/O error when initializing SQLite database at {Path}", DatabasePath);
            throw new InvalidOperationException($"I/O error accessing database at {DatabasePath}. Check disk space and file system health.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error initializing SQLite database at {Path}", DatabasePath);
            throw new InvalidOperationException($"Unexpected error initializing database at {DatabasePath}.", ex);
        }
    }

    /// <summary>
    /// Creates a new connection to the database.
    /// Caller is responsible for disposing the connection.
    /// </summary>
    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    /// <summary>
    /// Ensures a table exists for the given entity type with standard columns.
    /// </summary>
    public void EnsureTable<T>(string? tableName = null) where T : BaseEntity
    {
        var name = tableName ?? typeof(T).Name;

        try
        {
            using var connection = CreateConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS [{name}] (
                    Id TEXT PRIMARY KEY,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    Data TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_{name}_IsDeleted ON [{name}](IsDeleted);
            ";
            cmd.ExecuteNonQuery();

            _logger?.LogDebug("Ensured table {TableName} exists", name);
        }
        catch (SqliteException ex)
        {
            _logger?.LogError(ex, "Failed to ensure table {TableName} exists. Error code: {ErrorCode}", name, ex.SqliteErrorCode);
            throw new InvalidOperationException($"Failed to create or verify table {name}.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error ensuring table {TableName} exists", name);
            throw;
        }
    }

    /// <summary>
    /// Creates a custom table with specified schema.
    /// </summary>
    public void ExecuteNonQuery(string sql)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger?.LogDebug("SqliteDbContext disposed");
        }
        GC.SuppressFinalize(this);
    }
}
