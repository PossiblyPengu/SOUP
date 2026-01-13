using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SOUP.Core.Common;
using SOUP.Core.Interfaces;
using SOUP.Infrastructure.Data;

namespace SOUP.Infrastructure.Repositories;

/// <summary>
/// Base repository implementation using SQLite with JSON serialization for entity data.
/// </summary>
public class SqliteRepository<T> : IRepository<T> where T : BaseEntity
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    protected readonly SqliteDbContext Context;
    protected readonly string TableName;
    protected readonly ILogger<SqliteRepository<T>>? Logger;

    public SqliteRepository(SqliteDbContext context, ILogger<SqliteRepository<T>>? logger = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        TableName = typeof(T).Name;
        
        // Ensure table exists
        Context.EnsureTable<T>(TableName);
    }

    public virtual Task<T?> GetByIdAsync(Guid id)
    {
        try
        {
            using var connection = Context.CreateConnection();
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT Data FROM [{TableName}] WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id.ToString());
            
            var json = cmd.ExecuteScalar() as string;
            if (string.IsNullOrEmpty(json))
            {
                return Task.FromResult<T?>(null);
            }
            
            var entity = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return Task.FromResult(entity);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting entity by ID: {Id}", id);
            throw;
        }
    }

    public virtual Task<IEnumerable<T>> GetAllAsync()
    {
        try
        {
            using var connection = Context.CreateConnection();
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT Data FROM [{TableName}] WHERE IsDeleted = 0";
            
            var results = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var json = reader.GetString(0);
                var entity = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (entity is not null)
                {
                    results.Add(entity);
                }
            }
            
            return Task.FromResult<IEnumerable<T>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting all entities");
            throw;
        }
    }

    public virtual Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var compiledPredicate = predicate.Compile();
            
            using var connection = Context.CreateConnection();
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT Data FROM [{TableName}] WHERE IsDeleted = 0";
            
            var results = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var json = reader.GetString(0);
                var entity = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (entity is not null && compiledPredicate(entity))
                {
                    results.Add(entity);
                }
            }
            
            return Task.FromResult<IEnumerable<T>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error finding entities");
            throw;
        }
    }

    public virtual Task<T> AddAsync(T entity)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entity);
            
            using var connection = Context.CreateConnection();
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO [{TableName}] (Id, CreatedAt, UpdatedAt, IsDeleted, Data)
                VALUES (@Id, @CreatedAt, @UpdatedAt, @IsDeleted, @Data)
            ";
            cmd.Parameters.AddWithValue("@Id", entity.Id.ToString());
            cmd.Parameters.AddWithValue("@CreatedAt", entity.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@UpdatedAt", entity.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsDeleted", entity.IsDeleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(entity, JsonOptions));
            
            cmd.ExecuteNonQuery();
            return Task.FromResult(entity);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error adding entity");
            throw;
        }
    }

    public virtual Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entities);
            var entityList = entities.ToList();
            
            using var connection = Context.CreateConnection();
            connection.Open();
            
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var entity in entityList)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = $@"
                        INSERT INTO [{TableName}] (Id, CreatedAt, UpdatedAt, IsDeleted, Data)
                        VALUES (@Id, @CreatedAt, @UpdatedAt, @IsDeleted, @Data)
                    ";
                    cmd.Parameters.AddWithValue("@Id", entity.Id.ToString());
                    cmd.Parameters.AddWithValue("@CreatedAt", entity.CreatedAt.ToString("O"));
                    cmd.Parameters.AddWithValue("@UpdatedAt", entity.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsDeleted", entity.IsDeleted ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(entity, JsonOptions));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            
            return Task.FromResult<IEnumerable<T>>(entityList);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error adding entities in bulk");
            throw;
        }
    }

    public virtual Task<T> UpdateAsync(T entity)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entity);
            entity.UpdatedAt = DateTime.UtcNow;
            
            using var connection = Context.CreateConnection();
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                UPDATE [{TableName}]
                SET UpdatedAt = @UpdatedAt, IsDeleted = @IsDeleted, Data = @Data
                WHERE Id = @Id
            ";
            cmd.Parameters.AddWithValue("@Id", entity.Id.ToString());
            cmd.Parameters.AddWithValue("@UpdatedAt", entity.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsDeleted", entity.IsDeleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(entity, JsonOptions));
            
            cmd.ExecuteNonQuery();
            return Task.FromResult(entity);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error updating entity: {Id}", entity.Id);
            throw;
        }
    }

    public virtual Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            // Soft delete
            using var connection = Context.CreateConnection();
            connection.Open();
            
            // First get the entity to update
            T? entity;
            using (var getCmd = connection.CreateCommand())
            {
                getCmd.CommandText = $"SELECT Data FROM [{TableName}] WHERE Id = @Id";
                getCmd.Parameters.AddWithValue("@Id", id.ToString());
                var json = getCmd.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(json))
                {
                    return Task.FromResult(false);
                }
                entity = JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            
            if (entity is null)
            {
                return Task.FromResult(false);
            }
            
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                UPDATE [{TableName}]
                SET UpdatedAt = @UpdatedAt, IsDeleted = 1, Data = @Data
                WHERE Id = @Id
            ";
            cmd.Parameters.AddWithValue("@Id", id.ToString());
            cmd.Parameters.AddWithValue("@UpdatedAt", entity.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(entity, JsonOptions));
            
            var affected = cmd.ExecuteNonQuery();
            return Task.FromResult(affected > 0);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error deleting entity: {Id}", id);
            throw;
        }
    }

    public virtual Task DeleteAllAsync()
    {
        try
        {
            using var connection = Context.CreateConnection();
            connection.Open();
            
            // First get all entities to update their JSON
            var entities = new List<(Guid, T)>();
            using (var getCmd = connection.CreateCommand())
            {
                getCmd.CommandText = $"SELECT Id, Data FROM [{TableName}] WHERE IsDeleted = 0";
                using var reader = getCmd.ExecuteReader();
                while (reader.Read())
                {
                    var id = Guid.Parse(reader.GetString(0));
                    var json = reader.GetString(1);
                    var entity = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    if (entity is not null)
                    {
                        entities.Add((id, entity));
                    }
                }
            }
            
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var (id, entity) in entities)
                {
                    entity.IsDeleted = true;
                    entity.UpdatedAt = DateTime.UtcNow;
                    
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = $@"
                        UPDATE [{TableName}]
                        SET UpdatedAt = @UpdatedAt, IsDeleted = 1, Data = @Data
                        WHERE Id = @Id
                    ";
                    cmd.Parameters.AddWithValue("@Id", id.ToString());
                    cmd.Parameters.AddWithValue("@UpdatedAt", entity.UpdatedAt?.ToString("O") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Data", JsonSerializer.Serialize(entity, JsonOptions));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error deleting all entities");
            throw;
        }
    }

    public virtual Task<int> HardDeleteAllAsync()
    {
        try
        {
            using var connection = Context.CreateConnection();
            connection.Open();
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM [{TableName}]";
            var count = cmd.ExecuteNonQuery();
            
            Logger?.LogInformation("Hard deleted {Count} entities from table {TableName}", count, TableName);
            return Task.FromResult(count);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error hard deleting all entities");
            throw;
        }
    }
}
