using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.AllocationBuddy;

namespace SOUP.Services.AllocationBuddy;

/// <summary>
/// Service responsible for allocation calculation logic and item movement operations.
/// Handles core business logic without UI concerns.
/// </summary>
public class AllocationCalculationService
{
    private readonly ItemDictionaryService _dictionaryService;
    private readonly ILogger<AllocationCalculationService>? _logger;
    private readonly AllocationBuddyConfiguration _configuration;

    public AllocationCalculationService(
        ItemDictionaryService dictionaryService,
        AllocationBuddyConfiguration configuration,
        ILogger<AllocationCalculationService>? logger = null)
    {
        _dictionaryService = dictionaryService ?? throw new ArgumentNullException(nameof(dictionaryService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <summary>
    /// Removes one unit of an item from its location and adds it to the pool.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <param name="locations">The collection of location allocations.</param>
    /// <param name="itemPool">The item pool collection.</param>
    /// <returns>True if the operation succeeded.</returns>
    public bool RemoveOne(
        ItemAllocation item,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        if (item == null) return false;

        // Find the exact location containing this specific item instance
        var loc = locations.FirstOrDefault(l => l.Items.Contains(item));
        if (loc == null) return false;

        var target = loc.Items.First(i => ReferenceEquals(i, item));
        if (target.Quantity <= 0) return false;

        target.Quantity -= 1;

        // Add to pool
        var poolItem = itemPool.FirstOrDefault(p => p.ItemNumber == target.ItemNumber);
        if (poolItem == null)
        {
            var itemNumber = target.ItemNumber;
            var description = string.IsNullOrWhiteSpace(target.Description)
                ? _dictionaryService.GetDescription(itemNumber)
                : target.Description;
            var sku = target.SKU ?? _dictionaryService.GetSKU(itemNumber);

            itemPool.Add(new ItemAllocation
            {
                ItemNumber = itemNumber,
                Description = description,
                Quantity = 1,
                SKU = sku
            });
        }
        else
        {
            poolItem.Quantity += 1;
        }

        // Remove item if quantity is zero
        if (target.Quantity == 0)
        {
            loc.Items.Remove(target);
        }

        return true;
    }

    /// <summary>
    /// Adds one unit of an item from the pool to its location.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="locations">The collection of location allocations.</param>
    /// <param name="itemPool">The item pool collection.</param>
    /// <returns>True if the operation succeeded.</returns>
    public bool AddOne(
        ItemAllocation item,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        if (item == null) return false;

        // Check if pool has the item
        var poolItem = itemPool.FirstOrDefault(p => p.ItemNumber == item.ItemNumber);
        if (poolItem == null || poolItem.Quantity <= 0) return false;

        // Find the exact location containing this item instance
        var loc = locations.FirstOrDefault(l => l.Items.Contains(item));
        if (loc == null)
        {
            // Add to first location or create default location
            var itemNumber = _dictionaryService.GetCanonicalItemNumber(item.ItemNumber);
            var description = string.IsNullOrWhiteSpace(item.Description)
                ? _dictionaryService.GetDescription(itemNumber)
                : item.Description;
            var sku = item.SKU ?? _dictionaryService.GetSKU(itemNumber);

            if (locations.Count == 0)
            {
                // Create default location
                var newLoc = new LocationAllocation { Location = "Unassigned" };
                newLoc.Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = 1,
                    SKU = sku
                });
                locations.Add(newLoc);
            }
            else
            {
                locations[0].Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = 1,
                    SKU = sku
                });
            }
        }
        else
        {
            var target = loc.Items.First(i => ReferenceEquals(i, item));
            target.Quantity += 1;
        }

        poolItem.Quantity -= 1;
        if (poolItem.Quantity == 0)
        {
            itemPool.Remove(poolItem);
        }

        return true;
    }

    /// <summary>
    /// Moves all units of an item from the pool to the first location.
    /// </summary>
    /// <param name="item">The pool item to move.</param>
    /// <param name="locations">The collection of location allocations.</param>
    /// <param name="itemPool">The item pool collection.</param>
    /// <returns>True if the operation succeeded.</returns>
    public bool MoveFromPool(
        ItemAllocation item,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        if (item == null) return false;

        var poolItem = itemPool.FirstOrDefault(p => p.ItemNumber == item.ItemNumber);
        if (poolItem == null) return false;

        var qtyToMove = poolItem.Quantity;
        if (qtyToMove <= 0) return false;

        // Get canonical data
        var itemNumber = _dictionaryService.GetCanonicalItemNumber(poolItem.ItemNumber);
        var description = string.IsNullOrWhiteSpace(poolItem.Description)
            ? _dictionaryService.GetDescription(itemNumber)
            : poolItem.Description;
        var sku = poolItem.SKU ?? _dictionaryService.GetSKU(itemNumber);

        if (locations.Count == 0)
        {
            // Create default location
            var newLoc = new LocationAllocation { Location = "Unassigned" };
            newLoc.Items.Add(new ItemAllocation
            {
                ItemNumber = itemNumber,
                Description = description,
                Quantity = qtyToMove,
                SKU = sku
            });
            locations.Add(newLoc);
        }
        else
        {
            var firstLoc = locations[0];
            var existing = firstLoc.Items.FirstOrDefault(i => i.ItemNumber == itemNumber && i.SKU == sku);
            if (existing != null)
            {
                existing.Quantity += qtyToMove;
            }
            else
            {
                firstLoc.Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = qtyToMove,
                    SKU = sku
                });
            }
        }

        itemPool.Remove(poolItem);
        return true;
    }

    /// <summary>
    /// Deactivates a store by moving all its items to the pool.
    /// </summary>
    /// <param name="location">The location to deactivate.</param>
    /// <param name="itemPool">The item pool collection.</param>
    /// <returns>A deactivation record for undo functionality.</returns>
    public DeactivationRecord? DeactivateStore(
        LocationAllocation location,
        ObservableCollection<ItemAllocation> itemPool)
    {
        if (location == null) return null;

        // Create snapshot for undo
        var snapshot = location.Items
            .Select(i => new ItemSnapshot
            {
                ItemNumber = i.ItemNumber,
                Description = i.Description,
                Quantity = i.Quantity,
                SKU = i.SKU
            })
            .ToList();

        var deactivationRecord = new DeactivationRecord
        {
            Location = location,
            Items = snapshot
        };

        // Move all items from this location into the pool
        var items = location.Items.ToList();
        foreach (var it in items)
        {
            if (it == null) continue;

            var itemNumber = it.ItemNumber;
            var description = string.IsNullOrWhiteSpace(it.Description)
                ? _dictionaryService.GetDescription(itemNumber)
                : it.Description;
            var sku = it.SKU ?? _dictionaryService.GetSKU(itemNumber);

            var poolItem = itemPool.FirstOrDefault(p => p.ItemNumber == itemNumber && p.SKU == sku);
            if (poolItem == null)
            {
                itemPool.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = it.Quantity,
                    SKU = sku
                });
            }
            else
            {
                poolItem.Quantity += it.Quantity;
            }
        }

        // Clear the location's items and soft-disable
        location.Items.Clear();
        location.IsActive = false;

        return deactivationRecord;
    }

    /// <summary>
    /// Undoes a store deactivation by restoring items from the pool.
    /// </summary>
    /// <param name="record">The deactivation record to undo.</param>
    /// <param name="itemPool">The item pool collection.</param>
    /// <returns>True if the operation succeeded.</returns>
    public bool UndoDeactivate(
        DeactivationRecord record,
        ObservableCollection<ItemAllocation> itemPool)
    {
        if (record == null || record.Location == null) return false;

        var loc = record.Location;

        // Restore items from pool back to location where possible
        foreach (var snap in record.Items)
        {
            var poolItem = itemPool.FirstOrDefault(p => p.ItemNumber == snap.ItemNumber && p.SKU == snap.SKU);
            var qtyAvailable = poolItem?.Quantity ?? 0;
            var qtyToRestore = Math.Min(snap.Quantity, qtyAvailable);
            if (qtyToRestore <= 0) continue;

            var itemNumber = _dictionaryService.GetCanonicalItemNumber(snap.ItemNumber);
            var description = string.IsNullOrWhiteSpace(snap.Description)
                ? _dictionaryService.GetDescription(itemNumber)
                : snap.Description;
            var sku = snap.SKU ?? _dictionaryService.GetSKU(itemNumber);

            var existing = loc.Items.FirstOrDefault(i => i.ItemNumber == itemNumber && i.SKU == sku);
            if (existing == null)
            {
                loc.Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = qtyToRestore,
                    SKU = sku
                });
            }
            else
            {
                existing.Quantity += qtyToRestore;
            }

            if (poolItem != null)
            {
                poolItem.Quantity -= qtyToRestore;
                if (poolItem.Quantity <= 0)
                {
                    itemPool.Remove(poolItem);
                }
            }
        }

        loc.IsActive = true;
        return true;
    }

    /// <summary>
    /// Calculates item totals from locations and pool.
    /// </summary>
    /// <param name="locations">The collection of location allocations.</param>
    /// <param name="itemPool">The item pool collection.</param>
    /// <param name="sortMode">The sort mode to apply.</param>
    /// <returns>Sorted list of item total summaries.</returns>
    public List<ItemTotalSummary> CalculateItemTotals(
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool,
        string sortMode)
    {
        // Get items from locations (allocated)
        var locationItems = locations.SelectMany(l => l.Items).ToList();

        // Build lookup for pool quantities by item number
        var poolByItem = itemPool
            .GroupBy(p => p.ItemNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (Quantity: g.Sum(p => p.Quantity), Description: g.First().Description),
                StringComparer.OrdinalIgnoreCase);

        // Build lookup for location quantities by item number
        var locationByItem = locationItems
            .GroupBy(i => i.ItemNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (Quantity: g.Sum(i => i.Quantity), Description: g.First().Description, LocationCount: g.Count()),
                StringComparer.OrdinalIgnoreCase);

        // Get all unique item numbers from both sources
        var allItemNumbers = locationByItem.Keys
            .Union(poolByItem.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var grouped = allItemNumbers.Select(itemNum =>
        {
            var hasLocation = locationByItem.TryGetValue(itemNum, out var locData);
            var hasPool = poolByItem.TryGetValue(itemNum, out var poolData);

            return new ItemTotalSummary
            {
                ItemNumber = itemNum,
                Description = hasLocation ? locData.Description : (hasPool ? poolData.Description : ""),
                TotalQuantity = (hasLocation ? locData.Quantity : 0) + (hasPool ? poolData.Quantity : 0),
                LocationCount = hasLocation ? locData.LocationCount : 0,
                PoolQuantity = hasPool ? poolData.Quantity : 0
            };
        });

        // Apply sorting based on mode
        IEnumerable<ItemTotalSummary> sorted = sortMode switch
        {
            AllocationBuddyConstants.SortModes.QuantityAscending => grouped.OrderBy(t => t.TotalQuantity),
            AllocationBuddyConstants.SortModes.QuantityDescending => grouped.OrderByDescending(t => t.TotalQuantity),
            AllocationBuddyConstants.SortModes.ItemNumberAscending => grouped.OrderBy(t => t.ItemNumber),
            AllocationBuddyConstants.SortModes.ItemNumberDescending => grouped.OrderByDescending(t => t.ItemNumber),
            _ => grouped.OrderByDescending(t => t.TotalQuantity)
        };

        return sorted.ToList();
    }

    /// <summary>
    /// Builds item allocations view organized by item instead of by location.
    /// </summary>
    /// <param name="locations">The collection of location allocations.</param>
    /// <returns>List of item allocation views.</returns>
    public List<ItemAllocationView> BuildItemAllocationsView(ObservableCollection<LocationAllocation> locations)
    {
        // Get all items from all locations
        var allItems = locations
            .SelectMany(loc => loc.Items.Select(item => new { Location = loc, Item = item }))
            .ToList();

        // Group by item number
        var groupedByItem = allItems
            .GroupBy(x => x.Item.ItemNumber, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ItemAllocationView
            {
                ItemNumber = g.Key,
                Description = g.First().Item.Description,
                TotalQuantity = g.Sum(x => x.Item.Quantity),
                StoreAllocations = new ObservableCollection<StoreAllocation>(
                    g.Select(x => new StoreAllocation
                    {
                        StoreCode = x.Location.Location,
                        StoreName = x.Location.Location,
                        Quantity = x.Item.Quantity
                    }))
            })
            .OrderByDescending(v => v.TotalQuantity)
            .ToList();

        return groupedByItem;
    }

    /// <summary>
    /// Populates locations and pool from allocation entries.
    /// </summary>
    /// <param name="entries">The allocation entries to populate from.</param>
    /// <param name="locations">The collection of location allocations to populate.</param>
    /// <param name="itemPool">The item pool collection to populate.</param>
    public void PopulateFromEntries(
        IReadOnlyList<AllocationEntry> entries,
        ObservableCollection<LocationAllocation> locations,
        ObservableCollection<ItemAllocation> itemPool)
    {
        locations.Clear();
        itemPool.Clear();

        // Group entries by store/location
        var grouped = entries.GroupBy(e => e.StoreId ?? "Unallocated");

        foreach (var locGroup in grouped)
        {
            var locAlloc = new LocationAllocation
            {
                Location = locGroup.Key
            };

            foreach (var entry in locGroup)
            {
                var canonicalItemNumber = _dictionaryService.GetCanonicalItemNumber(entry.ItemNumber);
                var description = string.IsNullOrWhiteSpace(entry.Description)
                    ? _dictionaryService.GetDescription(canonicalItemNumber)
                    : entry.Description;
                var sku = entry.SKU ?? _dictionaryService.GetSKU(canonicalItemNumber);

                locAlloc.Items.Add(new ItemAllocation
                {
                    ItemNumber = canonicalItemNumber,
                    Description = description,
                    Quantity = entry.Quantity,
                    SKU = sku
                });
            }

            locations.Add(locAlloc);
        }
    }
}
