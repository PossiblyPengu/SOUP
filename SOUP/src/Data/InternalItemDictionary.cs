using System;
using System.Collections.Generic;
using System.Linq;
using SOUP.Data.Entities;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.Data;

/// <summary>
/// Shared item dictionary using SQLite for fast indexed lookups.
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
        return db.GetAllItems()
            .Select(e => new DictionaryItem
            {
                Number = e.Number,
                Description = e.Description,
                Skus = e.Skus,
                Tags = e.Tags ?? [],
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
        return DictionaryDbContext.Instance.GetItemCount();
    }

    /// <summary>
    /// Save/update items in the database
    /// </summary>
    public static void SaveItems(List<DictionaryItem> items)
    {
        var db = DictionaryDbContext.Instance;
        var entities = items.Select(item => new DictionaryItemEntity
        {
            Number = item.Number,
            Description = item.Description,
            Skus = item.Skus,
            Tags = item.Tags ?? [],
            IsEssential = item.IsEssential,
            IsPrivateLabel = item.IsPrivateLabel
        });
        db.UpsertItems(entities);
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
            Tags = item.Tags ?? [],
            IsEssential = item.IsEssential,
            IsPrivateLabel = item.IsPrivateLabel
        };
        DictionaryDbContext.Instance.UpsertItem(entity);
    }

    /// <summary>
    /// Delete an item by number
    /// </summary>
    public static bool DeleteItem(string number)
    {
        return DictionaryDbContext.Instance.DeleteItem(number);
    }

    /// <summary>
    /// Clear all items
    /// </summary>
    public static void ClearAll()
    {
        DictionaryDbContext.Instance.DeleteAllItems();
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

        var entity = DictionaryDbContext.Instance.GetItem(number.Trim());
        if (entity == null) return null;

        return new DictionaryItem
        {
            Number = entity.Number,
            Description = entity.Description,
            Skus = entity.Skus,
            Tags = entity.Tags ?? [],
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
        var entities = DictionaryDbContext.Instance.FindItems(
            x => x.Skus.Contains(term), 
            maxResults: 1);

        var entity = entities.FirstOrDefault();
        if (entity == null) return null;

        return new DictionaryItem
        {
            Number = entity.Number,
            Description = entity.Description,
            Skus = entity.Skus,
            Tags = entity.Tags ?? [],
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
        var entities = db.FindItems(x => x.Number.StartsWith(term, StringComparison.OrdinalIgnoreCase), maxResults: 1);
        var entity = entities.FirstOrDefault();
        if (entity != null)
        {
            return new DictionaryItem
            {
                Number = entity.Number,
                Description = entity.Description,
                Skus = entity.Skus,
                Tags = entity.Tags ?? [],
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
            return [];

        var term = searchTerm.Trim();

        return DictionaryDbContext.Instance
            .FindItems(x => x.Description.Contains(term, StringComparison.OrdinalIgnoreCase), maxResults)
            .Select(e => new DictionaryItem
            {
                Number = e.Number,
                Description = e.Description,
                Skus = e.Skus,
                Tags = e.Tags ?? [],
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
        return DictionaryDbContext.Instance.FindItems(x => x.IsEssential);
    }

    /// <summary>
    /// Get all essential items as DictionaryItem objects
    /// </summary>
    public static List<DictionaryItem> GetAllEssentialItems()
    {
        return DictionaryDbContext.Instance
            .FindItems(x => x.IsEssential)
            .Select(e => new DictionaryItem
            {
                Number = e.Number,
                Description = e.Description,
                Skus = e.Skus,
                Tags = e.Tags ?? [],
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

        var entity = DictionaryDbContext.Instance.GetItem(itemNumber.Trim());
        return entity?.IsEssential ?? false;
    }

    /// <summary>
    /// Check if item is marked as private label
    /// </summary>
    public static bool IsPrivateLabel(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber)) return false;

        var entity = DictionaryDbContext.Instance.GetItem(itemNumber.Trim());
        return entity?.IsPrivateLabel ?? false;
    }

    /// <summary>
    /// Get the full entity with all fields (including IsEssential, Tags)
    /// </summary>
    public static DictionaryItemEntity? GetEntity(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber)) return null;
        return DictionaryDbContext.Instance.GetItem(itemNumber.Trim());
    }

    /// <summary>
    /// Mark an item as essential
    /// </summary>
    public static void SetEssential(string itemNumber, bool isEssential)
    {
        var entity = DictionaryDbContext.Instance.GetItem(itemNumber.Trim());
        if (entity != null)
        {
            entity.IsEssential = isEssential;
            DictionaryDbContext.Instance.UpsertItem(entity);
        }
    }

    /// <summary>
    /// Bulk update essential status for multiple items
    /// </summary>
    public static void SetEssentialBulk(IEnumerable<string> itemNumbers, bool isEssential)
    {
        var db = DictionaryDbContext.Instance;
        var toUpdate = new List<DictionaryItemEntity>();

        foreach (var number in itemNumbers)
        {
            var entity = db.GetItem(number.Trim());
            if (entity != null)
            {
                entity.IsEssential = isEssential;
                toUpdate.Add(entity);
            }
        }

        if (toUpdate.Count > 0)
        {
            db.UpsertItems(toUpdate);
        }
    }

    /// <summary>
    /// Get count of essential items
    /// </summary>
    public static int GetEssentialCount()
    {
        return DictionaryDbContext.Instance.FindItems(x => x.IsEssential).Count;
    }
}
