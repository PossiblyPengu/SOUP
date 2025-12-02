using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using SAP.Core.Common;
using SAP.Core.Interfaces;
using SAP.Infrastructure.Data;

namespace SAP.Infrastructure.Repositories;

/// <summary>
/// Base repository implementation using LiteDB
/// </summary>
public class LiteDbRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly LiteDbContext Context;
    protected readonly ILiteCollection<T> Collection;
    protected readonly ILogger<LiteDbRepository<T>>? Logger;

    public LiteDbRepository(LiteDbContext context, ILogger<LiteDbRepository<T>>? logger = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = logger;
        Collection = context.GetCollection<T>();
    }

    public virtual Task<T?> GetByIdAsync(Guid id)
    {
        try
        {
            var result = Collection.FindById(new BsonValue(id));
            return Task.FromResult<T?>(result);
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
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted)
                .ToList();
            return Task.FromResult<IEnumerable<T>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting all entities");
            throw;
        }
    }

    public virtual Task<IEnumerable<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var compiledPredicate = predicate.Compile();
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted)
                .ToList()
                .Where(compiledPredicate)
                .ToList();
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
            Collection.Insert(entity);
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
            Collection.InsertBulk(entityList);
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
            Collection.Update(entity);
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
            var entity = Collection.FindById(new BsonValue(id));
            if (entity is not null)
            {
                entity.IsDeleted = true;
                entity.UpdatedAt = DateTime.UtcNow;
                Collection.Update(entity);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
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
            // Soft delete all
            var entities = Collection.Query().Where(x => !x.IsDeleted).ToList();
            foreach (var entity in entities)
            {
                entity.IsDeleted = true;
                entity.UpdatedAt = DateTime.UtcNow;
                Collection.Update(entity);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error deleting all entities");
            throw;
        }
    }
}
