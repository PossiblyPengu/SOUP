using System;
using System.Collections.Generic;
using System.Linq;
using SOUP.Data.Entities;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.Data;

/// <summary>
/// Shared store dictionary using LiteDB for fast indexed lookups.
/// Stores are saved in a shared database. Use the ImportDictionary tool to populate.
/// </summary>
public static class InternalStoreDictionary
{
    /// <summary>
    /// Get all stores from the database
    /// </summary>
    public static List<StoreEntry> GetStores()
    {
        return DictionaryDbContext.Instance.Stores
            .FindAll()
            .Select(e => new StoreEntry
            {
                Code = e.Code,
                Name = e.Name,
                Rank = e.Rank
            })
            .OrderBy(s => s.Code)
            .ToList();
    }

    /// <summary>
    /// Get the count of stores without loading all into memory
    /// </summary>
    public static int GetStoreCount()
    {
        return DictionaryDbContext.Instance.Stores.Count();
    }

    /// <summary>
    /// Save/update stores in the database
    /// </summary>
    public static void SaveStores(List<StoreEntry> stores)
    {
        var db = DictionaryDbContext.Instance;
        var collection = db.Stores;

        foreach (var store in stores)
        {
            var entity = new StoreEntity
            {
                Code = store.Code,
                Name = store.Name,
                Rank = store.Rank
            };
            collection.Upsert(entity);
        }
    }

    /// <summary>
    /// Add or update a single store
    /// </summary>
    public static void UpsertStore(StoreEntry store)
    {
        var entity = new StoreEntity
        {
            Code = store.Code,
            Name = store.Name,
            Rank = store.Rank
        };
        DictionaryDbContext.Instance.Stores.Upsert(entity);
    }

    /// <summary>
    /// Delete a store by code
    /// </summary>
    public static bool DeleteStore(string code)
    {
        return DictionaryDbContext.Instance.Stores.Delete(code);
    }

    /// <summary>
    /// Clear all stores
    /// </summary>
    public static void ClearAll()
    {
        DictionaryDbContext.Instance.Stores.DeleteAll();
    }

    /// <summary>
    /// Find a store by code (exact match, very fast with index)
    /// </summary>
    public static StoreEntry? FindByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var entity = DictionaryDbContext.Instance.Stores.FindById(code.Trim());
        if (entity == null) return null;

        return new StoreEntry
        {
            Code = entity.Code,
            Name = entity.Name,
            Rank = entity.Rank
        };
    }

    /// <summary>
    /// Find stores by name (partial match)
    /// </summary>
    public static List<StoreEntry> SearchByName(string searchTerm, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<StoreEntry>();

        var term = searchTerm.Trim();

        return DictionaryDbContext.Instance.Stores
            .Find(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .Select(e => new StoreEntry
            {
                Code = e.Code,
                Name = e.Name,
                Rank = e.Rank
            })
            .ToList();
    }

    /// <summary>
    /// Get stores by rank
    /// </summary>
    public static List<StoreEntry> GetByRank(string rank)
    {
        if (string.IsNullOrWhiteSpace(rank))
            return new List<StoreEntry>();

        return DictionaryDbContext.Instance.Stores
            .Find(x => x.Rank == rank.Trim().ToUpperInvariant())
            .Select(e => new StoreEntry
            {
                Code = e.Code,
                Name = e.Name,
                Rank = e.Rank
            })
            .OrderBy(s => s.Code)
            .ToList();
    }

    /// <summary>
    /// Get the database path for diagnostic purposes
    /// </summary>
    public static string GetDatabasePath() => DictionaryDbContext.DatabasePath;
}
