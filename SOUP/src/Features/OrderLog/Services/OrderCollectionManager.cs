using System.Collections.ObjectModel;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for managing the OrderLog item collections (active and archived).
/// Provides thread-safe operations with O(1) membership checks using HashSet tracking.
/// </summary>
public class OrderCollectionManager
{
    private readonly Lock _collectionLock = new();
    private readonly HashSet<Guid> _itemIds = new();
    private readonly HashSet<Guid> _archivedItemIds = new();

    public ObservableCollection<OrderItem> Items { get; }
    public ObservableCollection<OrderItem> ArchivedItems { get; }

    /// <summary>
    /// Gets all items (active + archived) in a single enumerable.
    /// </summary>
    public IEnumerable<OrderItem> AllItems => Items.Concat(ArchivedItems);

    public OrderCollectionManager(
        ObservableCollection<OrderItem> items,
        ObservableCollection<OrderItem> archivedItems)
    {
        Items = items;
        ArchivedItems = archivedItems;
    }

    /// <summary>
    /// Initializes the internal HashSet tracking from existing collections.
    /// Call this after loading items from persistence.
    /// </summary>
    public void InitializeTracking()
    {
        lock (_collectionLock)
        {
            _itemIds.Clear();
            _archivedItemIds.Clear();

            foreach (var item in Items)
            {
                _itemIds.Add(item.Id);
            }

            foreach (var item in ArchivedItems)
            {
                _archivedItemIds.Add(item.Id);
            }
        }
    }

    /// <summary>
    /// Clears all collections and tracking.
    /// </summary>
    public void Clear()
    {
        lock (_collectionLock)
        {
            Items.Clear();
            ArchivedItems.Clear();
            _itemIds.Clear();
            _archivedItemIds.Clear();
        }
    }

    #region Active Items Management

    /// <summary>
    /// Adds an item to the active Items collection with O(1) duplicate check.
    /// </summary>
    /// <param name="item">Item to add</param>
    /// <param name="insertAtTop">If true, inserts at index 0; otherwise appends to end</param>
    /// <returns>True if added, false if already exists</returns>
    public bool AddToItems(OrderItem item, bool insertAtTop = false)
    {
        lock (_collectionLock)
        {
            if (_itemIds.Contains(item.Id))
                return false;

            if (insertAtTop)
                Items.Insert(0, item);
            else
                Items.Add(item);

            _itemIds.Add(item.Id);
            return true;
        }
    }

    /// <summary>
    /// Removes an item from the active Items collection with O(1) existence check.
    /// </summary>
    /// <param name="item">Item to remove</param>
    /// <returns>True if removed, false if not found</returns>
    public bool RemoveFromItems(OrderItem item)
    {
        lock (_collectionLock)
        {
            if (!_itemIds.Contains(item.Id))
                return false;

            Items.Remove(item);
            _itemIds.Remove(item.Id);
            return true;
        }
    }

    /// <summary>
    /// Checks if an item exists in the active Items collection (O(1) operation).
    /// </summary>
    public bool ContainsInItems(OrderItem item)
    {
        lock (_collectionLock)
        {
            return _itemIds.Contains(item.Id);
        }
    }

    /// <summary>
    /// Checks if an item ID exists in the active Items collection (O(1) operation).
    /// </summary>
    public bool ContainsInItems(Guid itemId)
    {
        lock (_collectionLock)
        {
            return _itemIds.Contains(itemId);
        }
    }

    #endregion

    #region Archived Items Management

    /// <summary>
    /// Adds an item to the ArchivedItems collection with O(1) duplicate check.
    /// </summary>
    /// <param name="item">Item to add</param>
    /// <returns>True if added, false if already exists</returns>
    public bool AddToArchived(OrderItem item)
    {
        lock (_collectionLock)
        {
            if (_archivedItemIds.Contains(item.Id))
                return false;

            ArchivedItems.Add(item);
            _archivedItemIds.Add(item.Id);
            return true;
        }
    }

    /// <summary>
    /// Removes an item from the ArchivedItems collection with O(1) existence check.
    /// </summary>
    /// <param name="item">Item to remove</param>
    /// <returns>True if removed, false if not found</returns>
    public bool RemoveFromArchived(OrderItem item)
    {
        lock (_collectionLock)
        {
            if (!_archivedItemIds.Contains(item.Id))
                return false;

            ArchivedItems.Remove(item);
            _archivedItemIds.Remove(item.Id);
            return true;
        }
    }

    /// <summary>
    /// Checks if an item exists in the ArchivedItems collection (O(1) operation).
    /// </summary>
    public bool ContainsInArchived(OrderItem item)
    {
        lock (_collectionLock)
        {
            return _archivedItemIds.Contains(item.Id);
        }
    }

    /// <summary>
    /// Checks if an item ID exists in the ArchivedItems collection (O(1) operation).
    /// </summary>
    public bool ContainsInArchived(Guid itemId)
    {
        lock (_collectionLock)
        {
            return _archivedItemIds.Contains(itemId);
        }
    }

    #endregion

    #region Move Operations

    /// <summary>
    /// Moves an item from active to archived collection.
    /// </summary>
    /// <param name="item">Item to move</param>
    /// <returns>True if moved successfully</returns>
    public bool MoveToArchived(OrderItem item)
    {
        lock (_collectionLock)
        {
            if (!_itemIds.Contains(item.Id))
                return false;

            if (_archivedItemIds.Contains(item.Id))
                return false; // Already in archived

            Items.Remove(item);
            _itemIds.Remove(item.Id);

            ArchivedItems.Add(item);
            _archivedItemIds.Add(item.Id);
            return true;
        }
    }

    /// <summary>
    /// Moves an item from archived to active collection.
    /// </summary>
    /// <param name="item">Item to move</param>
    /// <param name="insertAtTop">If true, inserts at top of Items; otherwise appends</param>
    /// <returns>True if moved successfully</returns>
    public bool MoveToActive(OrderItem item, bool insertAtTop = false)
    {
        lock (_collectionLock)
        {
            if (!_archivedItemIds.Contains(item.Id))
                return false;

            if (_itemIds.Contains(item.Id))
                return false; // Already in active

            ArchivedItems.Remove(item);
            _archivedItemIds.Remove(item.Id);

            if (insertAtTop)
                Items.Insert(0, item);
            else
                Items.Add(item);

            _itemIds.Add(item.Id);
            return true;
        }
    }

    /// <summary>
    /// Moves multiple items from active to archived collection.
    /// </summary>
    public void MoveToArchived(IEnumerable<OrderItem> items)
    {
        foreach (var item in items)
        {
            MoveToArchived(item);
        }
    }

    /// <summary>
    /// Moves multiple items from archived to active collection.
    /// </summary>
    public void MoveToActive(IEnumerable<OrderItem> items, bool insertAtTop = false)
    {
        foreach (var item in items)
        {
            MoveToActive(item, insertAtTop);
        }
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets the total count of all items (active + archived).
    /// </summary>
    public int TotalCount
    {
        get
        {
            lock (_collectionLock)
            {
                return _itemIds.Count + _archivedItemIds.Count;
            }
        }
    }

    /// <summary>
    /// Gets the count of active items.
    /// </summary>
    public int ActiveCount
    {
        get
        {
            lock (_collectionLock)
            {
                return _itemIds.Count;
            }
        }
    }

    /// <summary>
    /// Gets the count of archived items.
    /// </summary>
    public int ArchivedCount
    {
        get
        {
            lock (_collectionLock)
            {
                return _archivedItemIds.Count;
            }
        }
    }

    #endregion
}
