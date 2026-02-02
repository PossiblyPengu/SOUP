using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SOUP.Data;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Service responsible for loading and managing item dictionary lookups for AllocationBuddy.
/// Provides O(1) lookup performance for item numbers and SKUs.
/// </summary>
public class ItemDictionaryService : IDisposable
{
    private readonly AllocationBuddyParser _parser;
    private readonly ILogger<ItemDictionaryService>? _logger;

    /// <summary>
    /// Thread-safe lookup dictionary for O(1) item access by item number.
    /// </summary>
    private ConcurrentDictionary<string, DictionaryItem> _itemLookupByNumber = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Thread-safe lookup dictionary for O(1) item access by SKU.
    /// </summary>
    private ConcurrentDictionary<string, DictionaryItem> _itemLookupBySku = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cancellation token source for background dictionary loading operations.
    /// </summary>
    private CancellationTokenSource? _loadDictionariesCts;

    private bool _disposed;

    public ItemDictionaryService(
        AllocationBuddyParser parser,
        ILogger<ItemDictionaryService>? logger = null)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _logger = logger;
    }

    /// <summary>
    /// Loads item and store dictionaries asynchronously from the shared location.
    /// </summary>
    public async Task LoadDictionariesAsync()
    {
        // Cancel any existing load operation
        _loadDictionariesCts?.Cancel();
        _loadDictionariesCts = new();
        var cancellationToken = _loadDictionariesCts.Token;

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Load store dictionary from shared location
                var stores = InternalStoreDictionary.GetStores();
                var mappedStores = stores.Select(s => new StoreEntry { Code = s.Code, Name = s.Name, Rank = s.Rank }).ToList();
                _parser.SetStoreDictionary(mappedStores);

                cancellationToken.ThrowIfCancellationRequested();

                // Load item dictionary from shared location
                var items = InternalItemDictionary.GetItems();
                _parser.SetDictionaryItems(items);

                cancellationToken.ThrowIfCancellationRequested();

                // Build lookup dictionaries for O(1) access
                BuildItemLookupDictionaries(items);

                _logger?.LogInformation("Loaded {Count} dictionary items for AllocationBuddy matching", items.Count);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Dictionary loading was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load dictionaries for AllocationBuddy");
        }
    }

    /// <summary>
    /// Gets the SKU for an item number using O(1) dictionary lookup.
    /// </summary>
    /// <param name="itemNumber">The item number to look up.</param>
    /// <returns>The first SKU if found, otherwise null.</returns>
    public string? GetSKU(string itemNumber)
    {
        var dictItem = FindDictionaryItem(itemNumber);
        return dictItem?.Skus?.FirstOrDefault();
    }

    /// <summary>
    /// Gets the description for an item number using O(1) dictionary lookup.
    /// </summary>
    /// <param name="itemNumber">The item number to look up.</param>
    /// <returns>The description if found, otherwise an empty string.</returns>
    public string GetDescription(string itemNumber)
    {
        var dictItem = FindDictionaryItem(itemNumber);
        return dictItem?.Description ?? "";
    }

    /// <summary>
    /// Gets the canonical item number using O(1) dictionary lookup.
    /// If the input is a SKU, returns the corresponding item number.
    /// </summary>
    /// <param name="itemNumber">The item number or SKU to look up.</param>
    /// <returns>The canonical item number if found, otherwise the input value.</returns>
    public string GetCanonicalItemNumber(string itemNumber)
    {
        var dictItem = FindDictionaryItem(itemNumber);
        return dictItem?.Number ?? itemNumber;
    }

    /// <summary>
    /// Finds a dictionary item by number or SKU using O(1) lookup.
    /// </summary>
    /// <param name="itemNumber">The item number or SKU to search for.</param>
    /// <returns>The dictionary item if found, otherwise null.</returns>
    public DictionaryItem? FindDictionaryItem(string itemNumber)
    {
        if (string.IsNullOrEmpty(itemNumber))
            return null;

        // Try lookup by item number first
        if (_itemLookupByNumber.TryGetValue(itemNumber, out var itemByNumber))
            return itemByNumber;

        // Try lookup by SKU
        if (_itemLookupBySku.TryGetValue(itemNumber, out var itemBySku))
            return itemBySku;

        return null;
    }

    /// <summary>
    /// Builds thread-safe lookup dictionaries from the item list for O(1) access.
    /// </summary>
    private void BuildItemLookupDictionaries(IReadOnlyList<DictionaryItem> items)
    {
        // Create new dictionaries to avoid mutation during reads
        var newByNumber = new ConcurrentDictionary<string, DictionaryItem>(StringComparer.OrdinalIgnoreCase);
        var newBySku = new ConcurrentDictionary<string, DictionaryItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            // Add by item number
            if (!string.IsNullOrEmpty(item.Number))
            {
                newByNumber.TryAdd(item.Number, item);
            }

            // Add by each SKU
            if (item.Skus != null)
            {
                foreach (var sku in item.Skus)
                {
                    if (!string.IsNullOrEmpty(sku))
                    {
                        newBySku.TryAdd(sku, item);
                    }
                }
            }
        }

        // Atomic swap of references (thread-safe due to reference assignment)
        _itemLookupByNumber = newByNumber;
        _itemLookupBySku = newBySku;
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _loadDictionariesCts?.Cancel();
            _loadDictionariesCts?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error disposing ItemDictionaryService");
        }

        _disposed = true;
    }
}
