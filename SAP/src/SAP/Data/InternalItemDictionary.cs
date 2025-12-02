using System;
using System.Collections.Generic;
using System.Linq;
using SAP.Data.Entities;
using SAP.Infrastructure.Services.Parsers;

namespace SAP.Data;

/// <summary>
/// Shared item dictionary using LiteDB for fast indexed lookups.
/// Items are stored in a shared database. Use the ImportDictionary tool to populate.
/// </summary>
public static class InternalItemDictionary
{
    /// <summary>
    /// Get all items from the database
    /// </summary>
    public static List<DictionaryItem> GetItems()
    {
        var db = DictionaryDbContext.Instance;
        return db.Items.FindAll()
            .Select(e => new DictionaryItem
            {
                Number = e.Number,
                Description = e.Description,
                Skus = e.Skus
            })
            .ToList();
    }

    /// <summary>
    /// Get the count of items without loading all into memory
    /// </summary>
    public static int GetItemCount()
    {
        return DictionaryDbContext.Instance.Items.Count();
    }

    /// <summary>
    /// Save/update items in the database
    /// </summary>
    public static void SaveItems(List<DictionaryItem> items)
    {
        var db = DictionaryDbContext.Instance;
        var collection = db.Items;

        foreach (var item in items)
        {
            var entity = new DictionaryItemEntity
            {
                Number = item.Number,
                Description = item.Description,
                Skus = item.Skus
            };
            collection.Upsert(entity);
        }
    }

    /// <summary>
    /// Add or update a single item
    /// </summary>
    public static void UpsertItem(DictionaryItem item)
    {
        var entity = new DictionaryItemEntity
        {
            Number = item.Number,
            Description = item.Description,
            Skus = item.Skus
        };
        DictionaryDbContext.Instance.Items.Upsert(entity);
    }

    /// <summary>
    /// Delete an item by number
    /// </summary>
    public static bool DeleteItem(string number)
    {
        return DictionaryDbContext.Instance.Items.Delete(number);
    }

    /// <summary>
    /// Clear all items
    /// </summary>
    public static void ClearAll()
    {
        DictionaryDbContext.Instance.Items.DeleteAll();
    }

    /// <summary>
    /// Get the database path for diagnostic purposes
    /// </summary>
    public static string GetDatabasePath() => DictionaryDbContext.DatabasePath;

    /// <summary>
    /// Find an item by its number (exact match, very fast with index)
    /// </summary>
    public static DictionaryItem? FindByNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;

        var entity = DictionaryDbContext.Instance.Items.FindById(number.Trim());
        if (entity == null) return null;

        return new DictionaryItem
        {
            Number = entity.Number,
            Description = entity.Description,
            Skus = entity.Skus
        };
    }

    /// <summary>
    /// Find an item by SKU
    /// </summary>
    public static DictionaryItem? FindBySku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;

        var term = sku.Trim();
        var entity = DictionaryDbContext.Instance.Items
            .FindOne(x => x.Skus.Contains(term));

        if (entity == null) return null;

        return new DictionaryItem
        {
            Number = entity.Number,
            Description = entity.Description,
            Skus = entity.Skus
        };
    }

    /// <summary>
    /// Find an item by its number, SKU, or partial match
    /// </summary>
    public static DictionaryItem? FindItem(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return null;

        var term = searchTerm.Trim();

        // Exact number match (fastest - uses primary key)
        var item = FindByNumber(term);
        if (item != null) return item;

        // SKU match
        item = FindBySku(term);
        if (item != null) return item;

        // Partial number match (starts with)
        var db = DictionaryDbContext.Instance;
        var entity = db.Items.FindOne(x => x.Number.StartsWith(term));
        if (entity != null)
        {
            return new DictionaryItem
            {
                Number = entity.Number,
                Description = entity.Description,
                Skus = entity.Skus
            };
        }

        return null;
    }

    /// <summary>
    /// Search items by description (partial match)
    /// </summary>
    public static List<DictionaryItem> SearchByDescription(string searchTerm, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<DictionaryItem>();

        var term = searchTerm.Trim().ToUpperInvariant();

        return DictionaryDbContext.Instance.Items
            .Find(x => x.Description.ToUpper().Contains(term))
            .Take(maxResults)
            .Select(e => new DictionaryItem
            {
                Number = e.Number,
                Description = e.Description,
                Skus = e.Skus
            })
            .ToList();
    }

    /// <summary>
    /// Get item description by number or SKU
    /// </summary>
    public static string GetDescription(string itemNumberOrSku)
    {
        var item = FindItem(itemNumberOrSku);
        return item?.Description ?? itemNumberOrSku;
    }
}
