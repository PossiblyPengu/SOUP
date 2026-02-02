using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.Services;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for archiving and unarchiving order items.
/// Handles both individual items and linked groups with timestamp synchronization.
/// </summary>
public class OrderArchiveService
{
    private readonly OrderTimestampSyncService _timestampSyncService;

    public OrderArchiveService(OrderTimestampSyncService timestampSyncService)
    {
        _timestampSyncService = timestampSyncService;
    }

    /// <summary>
    /// Result of an archive/unarchive operation.
    /// </summary>
    public class ArchiveOperationResult
    {
        public List<OrderItem> AffectedItems { get; set; } = new();
        public bool IsLinkedGroup { get; set; }
    }

    /// <summary>
    /// Determines which items should be affected by an archive/unarchive operation.
    /// If the item is part of a linked group, returns all items in that group.
    /// </summary>
    /// <param name="item">The item to check</param>
    /// <param name="allItems">All items (active + archived) to search within</param>
    /// <returns>List of items that will be affected</returns>
    public List<OrderItem> GetAffectedItems(OrderItem item, IEnumerable<OrderItem> allItems)
    {
        if (item.LinkedGroupId == null)
        {
            return new List<OrderItem> { item };
        }

        var groupId = item.LinkedGroupId.Value;
        return allItems.Where(i => i.LinkedGroupId == groupId).ToList();
    }

    /// <summary>
    /// Archives the specified items by setting their status to Done and IsArchived to true.
    /// For linked groups, synchronizes timestamps across all items.
    /// Creates and returns an ArchiveAction for undo support.
    /// </summary>
    /// <param name="items">Items to archive</param>
    /// <returns>ArchiveAction that can be executed and undone</returns>
    public ArchiveAction CreateArchiveAction(IReadOnlyList<OrderItem> items)
    {
        var action = new ArchiveAction(items);
        return action;
    }

    /// <summary>
    /// Executes the archive operation on the items and syncs timestamps for linked groups.
    /// </summary>
    /// <param name="items">Items to archive</param>
    public void ExecuteArchive(IReadOnlyList<OrderItem> items)
    {
        // Items will be marked as archived by the ArchiveAction
        // Just sync timestamps if it's a linked group
        if (items.Count > 1)
        {
            _timestampSyncService.SyncLinkedGroupTimestamps(items);
        }
    }

    /// <summary>
    /// Creates an unarchive action for the specified items.
    /// </summary>
    /// <param name="items">Items to unarchive</param>
    /// <returns>UnarchiveAction that can be executed and undone</returns>
    public UnarchiveAction CreateUnarchiveAction(IReadOnlyList<OrderItem> items)
    {
        var action = new UnarchiveAction(items);
        return action;
    }

    /// <summary>
    /// Transitions items to archived status by marking them Done.
    /// This is used when items are being archived through status change.
    /// </summary>
    /// <param name="items">Items to transition to archived</param>
    /// <param name="undoRedoStack">Undo/redo stack for tracking the action</param>
    /// <returns>Operation result with affected items</returns>
    public ArchiveOperationResult TransitionToArchived(
        IReadOnlyList<OrderItem> items,
        UndoRedoStack undoRedoStack)
    {
        var action = CreateArchiveAction(items);
        undoRedoStack.ExecuteAction(action);

        // Sync timestamps for linked groups
        if (items.Count > 1)
        {
            _timestampSyncService.SyncLinkedGroupTimestamps(items);
        }

        return new ArchiveOperationResult
        {
            AffectedItems = items.ToList(),
            IsLinkedGroup = items.Count > 1
        };
    }

    /// <summary>
    /// Transitions items from archived back to active status.
    /// </summary>
    /// <param name="items">Items to unarchive</param>
    /// <param name="undoRedoStack">Undo/redo stack for tracking the action</param>
    /// <returns>Operation result with affected items</returns>
    public ArchiveOperationResult TransitionToActive(
        IReadOnlyList<OrderItem> items,
        UndoRedoStack undoRedoStack)
    {
        var action = CreateUnarchiveAction(items);
        undoRedoStack.ExecuteAction(action);

        return new ArchiveOperationResult
        {
            AffectedItems = items.ToList(),
            IsLinkedGroup = items.Count > 1
        };
    }

    /// <summary>
    /// Archives a single item or its linked group.
    /// Handles both the undo action and timestamp synchronization.
    /// </summary>
    /// <param name="item">Item to archive</param>
    /// <param name="allItems">All items to search for linked group members</param>
    /// <param name="undoRedoStack">Undo/redo stack</param>
    /// <returns>Operation result</returns>
    public ArchiveOperationResult ArchiveItem(
        OrderItem item,
        IEnumerable<OrderItem> allItems,
        UndoRedoStack undoRedoStack)
    {
        var itemsToArchive = GetAffectedItems(item, allItems);
        return TransitionToArchived(itemsToArchive, undoRedoStack);
    }

    /// <summary>
    /// Unarchives a single item or its linked group.
    /// Handles both the undo action and collection management.
    /// </summary>
    /// <param name="item">Item to unarchive</param>
    /// <param name="allItems">All items to search for linked group members</param>
    /// <param name="undoRedoStack">Undo/redo stack</param>
    /// <returns>Operation result</returns>
    public ArchiveOperationResult UnarchiveItem(
        OrderItem item,
        IEnumerable<OrderItem> allItems,
        UndoRedoStack undoRedoStack)
    {
        var itemsToUnarchive = GetAffectedItems(item, allItems);
        return TransitionToActive(itemsToUnarchive, undoRedoStack);
    }
}
