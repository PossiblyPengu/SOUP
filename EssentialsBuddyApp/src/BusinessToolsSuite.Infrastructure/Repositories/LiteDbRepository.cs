using BusinessToolsSuite.Infrastructure.Data;

namespace BusinessToolsSuite.Infrastructure.Repositories;

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

    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = Collection.FindById(new BsonValue(id));
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting entity by ID: {Id}", id);
            throw;
        }
    }

    public virtual Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted)
                .ToList();
            return Task.FromResult<IReadOnlyList<T>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error getting all entities");
            throw;
        }
    }

    public virtual Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
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

    public virtual Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
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

    public virtual Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Soft delete
            var entity = Collection.FindById(new BsonValue(id));
            if (entity is null)
                return Task.FromResult(false);

            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            Collection.Update(entity);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error deleting entity: {Id}", id);
            throw;
        }
    }

    public virtual Task<IReadOnlyList<T>> FindAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var results = Collection
                .Query()
                .Where(x => !x.IsDeleted)
                .ToList()
                .Where(predicate)
                .ToList();
            return Task.FromResult<IReadOnlyList<T>>(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error finding entities");
            throw;
        }
    }

    public virtual Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = Collection.Query().Where(x => !x.IsDeleted).Count();
            return Task.FromResult(count);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error counting entities");
            throw;
        }
    }

    public virtual Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = Collection.Exists(x => x.Id == id && !x.IsDeleted);
            return Task.FromResult(exists);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error checking entity existence: {Id}", id);
            throw;
        }
    }
}
