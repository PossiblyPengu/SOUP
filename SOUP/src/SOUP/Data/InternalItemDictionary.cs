using System;
using System.Collections.Generic;
using System.Linq;
using SOUP.Data.Entities;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.Data;

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
                Skus = e.Skus,
                Tags = e.Tags ?? new List<string>(),
                IsEssential = e.IsEssential,
                IsPrivateLabel = e.IsPrivateLabel
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
                Skus = item.Skus,
                Tags = item.Tags ?? new List<string>(),
                IsEssential = item.IsEssential,
                IsPrivateLabel = item.IsPrivateLabel
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
            Skus = item.Skus,
            Tags = item.Tags ?? new List<string>(),
            IsEssential = item.IsEssential,
            IsPrivateLabel = item.IsPrivateLabel
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
            Skus = entity.Skus,
            Tags = entity.Tags ?? new List<string>(),
            IsEssential = entity.IsEssential,
            IsPrivateLabel = entity.IsPrivateLabel
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
            Skus = entity.Skus,
            Tags = entity.Tags ?? new List<string>(),
            IsEssential = entity.IsEssential,
            IsPrivateLabel = entity.IsPrivateLabel
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
                Skus = entity.Skus,
                Tags = entity.Tags ?? new List<string>(),
                IsEssential = entity.IsEssential,
                IsPrivateLabel = entity.IsPrivateLabel
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

        var term = searchTerm.Trim();

        return DictionaryDbContext.Instance.Items
            .Find(x => x.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .Select(e => new DictionaryItem
            {
                Number = e.Number,
                Description = e.Description,
                Skus = e.Skus,
                Tags = e.Tags ?? new List<string>(),
                IsEssential = e.IsEssential,
                IsPrivateLabel = e.IsPrivateLabel
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

    /// <summary>
    /// Get all items marked as essential
    /// </summary>
    public static List<DictionaryItemEntity> GetEssentialItems()
    {
        return DictionaryDbContext.Instance.Items
            .Find(x => x.IsEssential)
            .ToList();
    }

    /// <summary>
    /// Get all essential items as DictionaryItem objects
    /// </summary>
    public static List<DictionaryItem> GetAllEssentialItems()
    {
        return DictionaryDbContext.Instance.Items
            .Find(x => x.IsEssential)
            .Select(e => new DictionaryItem
            {
                Number = e.Number,
                Description = e.Description,
                Skus = e.Skus,
                Tags = e.Tags ?? new List<string>(),
                IsEssential = e.IsEssential,
                IsPrivateLabel = e.IsPrivateLabel
            })
            .ToList();
    }

    /// <summary>
    /// Check if an item is marked as essential
    /// </summary>
    public static bool IsItemEssential(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber)) return false;
        
        var entity = DictionaryDbContext.Instance.Items.FindById(itemNumber.Trim());
        return entity?.IsEssential ?? false;
    }

    /// <summary>
    /// Check if item is marked as private label
    /// </summary>
    public static bool IsPrivateLabel(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber)) return false;
        
        var entity = DictionaryDbContext.Instance.Items.FindById(itemNumber.Trim());
        return entity?.IsPrivateLabel ?? false;
    }

    /// <summary>
    /// Get the full entity with all fields (including IsEssential, Tags)
    /// </summary>
    public static DictionaryItemEntity? GetEntity(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber)) return null;
        return DictionaryDbContext.Instance.Items.FindById(itemNumber.Trim());
    }

    /// <summary>
    /// Mark an item as essential
    /// </summary>
    public static void SetEssential(string itemNumber, bool isEssential)
    {
        var entity = DictionaryDbContext.Instance.Items.FindById(itemNumber.Trim());
        if (entity != null)
        {
            entity.IsEssential = isEssential;
            DictionaryDbContext.Instance.Items.Update(entity);
        }
    }

    /// <summary>
    /// Bulk update essential status for multiple items
    /// </summary>
    public static void SetEssentialBulk(IEnumerable<string> itemNumbers, bool isEssential)
    {
        var db = DictionaryDbContext.Instance;
        foreach (var number in itemNumbers)
        {
            var entity = db.Items.FindById(number.Trim());
            if (entity != null)
            {
                entity.IsEssential = isEssential;
                db.Items.Update(entity);
            }
        }
    }

    /// <summary>
    /// Get count of essential items
    /// </summary>
    public static int GetEssentialCount()
    {
        return DictionaryDbContext.Instance.Items.Count(x => x.IsEssential);
    }
}
