using System;
using System.Collections.Generic;
using System.Linq;
using SOUP.Data;
using SOUP.Data.Entities;

namespace SOUP.Features.ExpireWise.Services;

/// <summary>
/// Service for looking up items in the Business Central dictionary.
/// Provides efficient search methods for ExpireWise SKU validation.
/// </summary>
public class ItemLookupService
{
    private readonly DictionaryDbContext _dictionaryDb;

    public ItemLookupService()
    {
        _dictionaryDb = DictionaryDbContext.Instance;
    }

    /// <summary>
    /// Lookup result containing the matched item and match details
    /// </summary>
    public class LookupResult
    {
        public bool Found { get; set; }
        public DictionaryItemEntity? Item { get; set; }
        public string? MatchedOn { get; set; } // "ItemNumber" or "SKU:xxx"
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Search for an item by SKU, item number, or description.
    /// Searches in order: exact item number → SKU match → nothing found
    /// </summary>
    public LookupResult Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new LookupResult
            {
                Found = false,
                ErrorMessage = "Search query cannot be empty"
            };
        }

        query = query.Trim();

        try
        {
            // First, try exact match on item number
            var itemByNumber = _dictionaryDb.GetItem(query);
            if (itemByNumber != null)
            {
                return new LookupResult
                {
                    Found = true,
                    Item = itemByNumber,
                    MatchedOn = "ItemNumber"
                };
            }

            // Second, search through SKUs (requires loading all items and filtering - not ideal for large datasets)
            // In-memory search for SKU match
            var itemBySku = _dictionaryDb.FindItems(
                item => item.Skus.Any(sku => sku.Equals(query, StringComparison.OrdinalIgnoreCase)),
                maxResults: 1
            ).FirstOrDefault();

            if (itemBySku != null)
            {
                var matchedSku = itemBySku.Skus.First(sku => sku.Equals(query, StringComparison.OrdinalIgnoreCase));
                return new LookupResult
                {
                    Found = true,
                    Item = itemBySku,
                    MatchedOn = $"SKU:{matchedSku}"
                };
            }

            // Not found
            return new LookupResult
            {
                Found = false,
                ErrorMessage = "Item not found in Business Central dictionary"
            };
        }
        catch (Exception ex)
        {
            return new LookupResult
            {
                Found = false,
                ErrorMessage = $"Lookup error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Batch lookup for multiple SKUs.
    /// Returns dictionary of query → lookup result.
    /// </summary>
    public Dictionary<string, LookupResult> BatchSearch(IEnumerable<string> queries)
    {
        var results = new Dictionary<string, LookupResult>();

        foreach (var query in queries)
        {
            results[query] = Search(query);
        }

        return results;
    }

    /// <summary>
    /// Get statistics about the dictionary
    /// </summary>
    public DictionaryStats GetStats()
    {
        return new DictionaryStats
        {
            ItemCount = _dictionaryDb.GetItemCount(),
            StoreCount = _dictionaryDb.GetStoreCount(),
            HasData = _dictionaryDb.HasItems && _dictionaryDb.HasStores
        };
    }

    /// <summary>
    /// Dictionary statistics
    /// </summary>
    public class DictionaryStats
    {
        public int ItemCount { get; set; }
        public int StoreCount { get; set; }
        public bool HasData { get; set; }
    }
}
