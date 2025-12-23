using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.Constants;
using SOUP.Features.OrderLog.Services;

namespace SOUP.Features.OrderLog.ViewModels;

public partial class OrderLogViewModel : ObservableObject, IDisposable
{
    private readonly IOrderLogService _orderLogService;
    private readonly GroupStateStore _groupStateStore;
    private readonly ILogger<OrderLogViewModel>? _logger;
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    private DispatcherTimer? _undoTimer;
    private List<(Guid id, OrderItem.OrderStatus previous)> _lastStatusChanges = new();

    public ObservableCollection<OrderItem> Items { get; } = new();
    public ObservableCollection<OrderItem> ArchivedItems { get; } = new();
    public ObservableCollection<OrderItem> SelectedItems { get; } = new();
    public ObservableCollection<OrderItemGroup> DisplayItems { get; } = new();

    [ObservableProperty]
    private bool _showArchived = false;

    [ObservableProperty]
    private OrderItem? _selectedItem;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _undoAvailable;

    [ObservableProperty]
    private string _undoMessage = string.Empty;

    [ObservableProperty]
    private string _newNoteVendorName = string.Empty;

    [ObservableProperty]
    private string _newNoteTransferNumbers = string.Empty;

    [ObservableProperty]
    private string _newNoteWhsShipmentNumbers = string.Empty;

    [ObservableProperty]
    private string _stickyNoteContent = string.Empty;

    private string _newNoteColorHex = OrderLogColors.DefaultOrder;
    private string _stickyNoteColorHex = OrderLogColors.DefaultNote; // Yellow default for sticky notes

    public event Action? GroupStatesReset;

    public OrderLogViewModel(
        IOrderLogService orderLogService,
        GroupStateStore groupStateStore,
        ILogger<OrderLogViewModel>? logger = null)
    {
        _orderLogService = orderLogService;
        _groupStateStore = groupStateStore;
        _logger = logger;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _logger?.LogInformation("OrderLogViewModel initialized");
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshTimeInProgress();
        }
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading orders...";

        try
        {
            var items = await _orderLogService.LoadAsync();
            Items.Clear();
            ArchivedItems.Clear();
            
            foreach (var item in items)
            {
                if (item.IsArchived)
                    ArchivedItems.Add(item);
                else
                    Items.Add(item);
            }
            
            StatusMessage = $"Loaded {Items.Count} orders ({ArchivedItems.Count} archived)";
            RefreshDisplayItems();
            _logger?.LogInformation("Loaded {Count} orders into view", items.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load orders: {ex.Message}";
            _logger?.LogError(ex, "Failed to load orders");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].Order = i;
            }
            
            for (int i = 0; i < ArchivedItems.Count; i++)
            {
                ArchivedItems[i].Order = Items.Count + i;
            }
            
            var allItems = Items.Concat(ArchivedItems).ToList();
            await _orderLogService.SaveAsync(allItems);
            StatusMessage = $"Saved {Items.Count} orders ({ArchivedItems.Count} archived)";
            RefreshDisplayItems();
            _logger?.LogInformation("Saved {Count} orders", allItems.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            _logger?.LogError(ex, "Failed to save orders");
        }
    }

    [RelayCommand]
    private async Task ArchiveOrderAsync(OrderItem? item)
    {
        if (item == null) return;
        
        item.IsArchived = true;
        Items.Remove(item);
        ArchivedItems.Add(item);
        await SaveAsync();
        StatusMessage = "Order archived";
    }

    [RelayCommand]
    private async Task UnarchiveOrderAsync(OrderItem? item)
    {
        if (item == null) return;
        
        // Prevent duplication if already in Items
        if (Items.Contains(item)) return;
        
        item.IsArchived = false;
        ArchivedItems.Remove(item);
        Items.Insert(0, item);
        await SaveAsync();
        StatusMessage = "Order restored";
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        var count = SelectedItems.Count;
        var toRemove = SelectedItems.ToList();
        foreach (var item in toRemove)
        {
            Items.Remove(item);
            ArchivedItems.Remove(item);
        }
        SelectedItems.Clear();

        await SaveAsync();
        StatusMessage = $"Deleted {count} order(s)";
    }

    [RelayCommand]
    public async Task MoveUpAsync(OrderItem? item)
    {
        if (item == null) return;

        // Try move within active items
        var idx = Items.IndexOf(item);
        if (idx > 0)
        {
            Items.Move(idx, idx - 1);
            await SaveAsync();
            StatusMessage = "Moved up";
            return;
        }

        // Try archived list
        var aidx = ArchivedItems.IndexOf(item);
        if (aidx > 0)
        {
            ArchivedItems.Move(aidx, aidx - 1);
            await SaveAsync();
            StatusMessage = "Moved up (archived)";
        }
    }

    [RelayCommand]
    public async Task MoveDownAsync(OrderItem? item)
    {
        if (item == null) return;

        // active items
        var idx = Items.IndexOf(item);
        if (idx >= 0 && idx < Items.Count - 1)
        {
            Items.Move(idx, idx + 1);
            await SaveAsync();
            StatusMessage = "Moved down";
            return;
        }

        // archived items
        var aidx = ArchivedItems.IndexOf(item);
        if (aidx >= 0 && aidx < ArchivedItems.Count - 1)
        {
            ArchivedItems.Move(aidx, aidx + 1);
            await SaveAsync();
            StatusMessage = "Moved down (archived)";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(OrderItem? item)
    {
        if (item == null) return;

        Items.Remove(item);
        ArchivedItems.Remove(item);
        await SaveAsync();
        StatusMessage = "Deleted item";
    }

    [RelayCommand]
    private async Task BulkSetColorAsync(string? colorHex)
    {
        if (SelectedItems.Count == 0 || string.IsNullOrEmpty(colorHex)) return;

        foreach (var item in SelectedItems)
        {
            item.ColorHex = colorHex;
        }
        await SaveAsync();
    }

    [RelayCommand]
    private async Task BulkSetStatusAsync(OrderItem.OrderStatus status)
    {
        if (SelectedItems.Count == 0) return;

        var changes = new List<(Guid id, OrderItem.OrderStatus previous)>();
        foreach (var item in SelectedItems)
        {
            changes.Add((item.Id, item.Status));
            item.Status = status;
        }

        await SaveAsync();

        SetupUndo(changes, $"Set {SelectedItems.Count} item(s) to {status}");
    }

    

    private void SetupUndo(List<(Guid id, OrderItem.OrderStatus previous)> changes, string message)
    {
        _lastStatusChanges = changes;
        UndoMessage = message;
        UndoAvailable = true;

        _undoTimer?.Stop();
        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _undoTimer.Tick += (s, e) =>
        {
            UndoAvailable = false;
            _lastStatusChanges.Clear();
            _undoTimer?.Stop();
        };
        _undoTimer.Start();
    }

    [RelayCommand]
    private async Task UndoStatusChangeAsync()
    {
        if (_lastStatusChanges == null || _lastStatusChanges.Count == 0) return;

        foreach (var (id, prev) in _lastStatusChanges)
        {
            var item = Items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                item.Status = prev;
            }
        }

        await SaveAsync();
        _lastStatusChanges.Clear();
        UndoAvailable = false;
        _undoTimer?.Stop();
        StatusMessage = "Undo applied";
    }

    [RelayCommand]
    private async Task BulkStartAsync()
    {
        if (SelectedItems.Count == 0) return;
        foreach (var item in SelectedItems)
        {
            item.Status = OrderItem.OrderStatus.InProgress;
        }
        await SaveAsync();
        StatusMessage = $"Started {SelectedItems.Count} order(s)";
    }

    [RelayCommand]
    private async Task BulkStopAsync()
    {
        if (SelectedItems.Count == 0) return;
        foreach (var item in SelectedItems)
        {
            item.Status = OrderItem.OrderStatus.Done;
        }
        await SaveAsync();
        StatusMessage = $"Stopped {SelectedItems.Count} order(s)";
    }

    [RelayCommand]
    public async Task StartAsync(OrderItem? item)
    {
        if (item == null) return;
        item.Status = OrderItem.OrderStatus.InProgress;
        await SaveAsync();
    }

    [RelayCommand]
    public async Task StopAsync(OrderItem? item)
    {
        if (item == null) return;
        item.Status = OrderItem.OrderStatus.Done;
        await SaveAsync();
    }

    [RelayCommand]
    private void ResetGroups()
    {
        _groupStateStore.ResetAll();
        GroupStatesReset?.Invoke();
        StatusMessage = "Group states reset";
    }

    public async Task SetColorAsync(OrderItem item, string colorHex)
    {
        item.ColorHex = colorHex;
        await SaveAsync();
    }

    public async Task SetStatusAsync(OrderItem item, OrderItem.OrderStatus status)
    {
        if (item == null) return;
        
        var wasDone = item.Status == OrderItem.OrderStatus.Done;
        var willBeDone = status == OrderItem.OrderStatus.Done;
        
        item.Status = status;
        
        // Move between collections based on archive status
        if (willBeDone && !wasDone)
        {
            // Moving to Done - archive it
            Items.Remove(item);
            ArchivedItems.Add(item);
        }
        else if (!willBeDone && wasDone)
        {
            // Moving away from Done - unarchive it
            item.IsArchived = false;
            ArchivedItems.Remove(item);
            Items.Insert(0, item);
        }
        
        await SaveAsync();
    }

    public async Task AddOrderAsync(OrderItem order)
    {
        Items.Insert(0, order);
        await SaveAsync();
        StatusMessage = "Order added";
    }

    public async Task<bool> AddOrderInlineAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteVendorName))
        {
            StatusMessage = "Vendor name is required";
            return false;
        }

        var order = new OrderItem
        {
            VendorName = NewNoteVendorName.Trim(),
            TransferNumbers = NewNoteTransferNumbers?.Trim() ?? string.Empty,
            WhsShipmentNumbers = NewNoteWhsShipmentNumbers?.Trim() ?? string.Empty,
            ColorHex = _newNoteColorHex,
            CreatedAt = DateTime.UtcNow
        };

        Items.Insert(0, order);
        await SaveAsync();

        // Clear the form
        NewNoteVendorName = string.Empty;
        NewNoteTransferNumbers = string.Empty;
        NewNoteWhsShipmentNumbers = string.Empty;
        _newNoteColorHex = OrderLogColors.DefaultOrder;

        StatusMessage = "Order added";
        return true;
    }

    /// <summary>
    /// Add a sticky note (simple note without order tracking)
    /// </summary>
    public async Task<bool> AddStickyNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(StickyNoteContent))
        {
            StatusMessage = "Note content is required";
            return false;
        }

        var note = new OrderItem
        {
            NoteType = NoteType.StickyNote,
            NoteTitle = string.Empty,
            NoteContent = StickyNoteContent.Trim(),
            ColorHex = _stickyNoteColorHex,
            CreatedAt = DateTime.UtcNow,
            Status = OrderItem.OrderStatus.OnDeck // Sticky notes start as "On Deck" (yellow)
        };

        Items.Insert(0, note);
        await SaveAsync();

        // Clear the form
        StickyNoteContent = string.Empty;
        _stickyNoteColorHex = OrderLogColors.DefaultNote;

        StatusMessage = "Sticky note added";
        return true;
    }

    /// <summary>
    /// Quick add a sticky note with specified content
    /// </summary>
    public async Task AddQuickStickyNoteAsync(string content, string? colorHex = null)
    {
        var note = new OrderItem
        {
            NoteType = NoteType.StickyNote,
            NoteTitle = string.Empty,
            NoteContent = content.Trim(),
            ColorHex = colorHex ?? OrderLogColors.DefaultNote,
            CreatedAt = DateTime.UtcNow,
            Status = OrderItem.OrderStatus.OnDeck
        };

        Items.Insert(0, note);
        await SaveAsync();
        StatusMessage = "Quick note added";
    }

    public void SetStickyNoteColor(string colorHex)
    {
        _stickyNoteColorHex = colorHex;
    }

    public string GetStickyNoteColor() => _stickyNoteColorHex;

    public void SetNewNoteColor(string colorHex)
    {
        _newNoteColorHex = colorHex;
    }

    public string GetNewNoteColor() => _newNoteColorHex;

    [RelayCommand]
    private void ToggleArchived()
    {
        ShowArchived = !ShowArchived;
    }

    [RelayCommand]
    private async Task ExportToCsv()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Exporting to CSV...";

            var exportService = new Services.OrderLogExportService();
            var itemsToExport = ShowArchived ? ArchivedItems : Items;
            var filePath = await exportService.ExportToCsvAsync(itemsToExport);

            var fileName = System.IO.Path.GetFileName(filePath);
            StatusMessage = $"Exported {itemsToExport.Count} item(s) to {fileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _logger?.LogError(ex, "Failed to export to CSV");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task MoveOrderAsync(OrderItem dropped, OrderItem? target)
    {
        if (dropped == target) return;

        if (target == null)
        {
            if (Items.Contains(dropped))
            {
                Items.Remove(dropped);
                Items.Add(dropped);
            }
        }
        else
        {
            int oldIndex = Items.IndexOf(dropped);
            int newIndex = Items.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0) return;

            Items.RemoveAt(oldIndex);
            if (oldIndex < newIndex) newIndex--;
            Items.Insert(newIndex, dropped);
        }

        await SaveAsync();
    }

    /// <summary>
    /// Move one or more orders as a block. Preserves relative order of moved items.
    /// If any item belongs to a linked group and the dragged set is a single item,
    /// the entire linked group will be moved together.
    /// </summary>
    public async Task MoveOrdersAsync(System.Collections.Generic.List<OrderItem> droppedItems, OrderItem? target)
    {
        if (droppedItems == null || droppedItems.Count == 0) return;

        // If single item and it has a linked group, expand to full group
        if (droppedItems.Count == 1 && droppedItems[0].LinkedGroupId != null)
        {
            var gid = droppedItems[0].LinkedGroupId;
            var groupMembers = Items.Concat(ArchivedItems).Where(i => i.LinkedGroupId == gid).ToList();
            if (groupMembers.Count > 1)
            {
                droppedItems = groupMembers;
            }
        }

        // Determine target collection based on where items/target are located
        bool operateOnItems = droppedItems.Any(d => Items.Contains(d));
        if (target != null)
        {
            operateOnItems = Items.Contains(target);
        }

        // Perform the move operation on the appropriate collection
        var collection = operateOnItems ? Items : ArchivedItems;
        MoveItemsInCollection(droppedItems, target, collection);

        await SaveAsync();
        RefreshDisplayItems();
    }

    /// <summary>
    /// Helper method to move items within a collection while preserving their relative order.
    /// </summary>
    private void MoveItemsInCollection(List<OrderItem> droppedItems, OrderItem? target, ObservableCollection<OrderItem> collection)
    {
        // Remove items from collection, preserving the order they appear in the collection
        var toInsert = droppedItems
            .Where(d => collection.Contains(d))
            .OrderBy(d => collection.IndexOf(d))
            .ToList();

        foreach (var item in toInsert)
        {
            collection.Remove(item);
        }

        // Calculate insertion index and insert items
        int insertIndex = target == null ? collection.Count : Math.Max(0, collection.IndexOf(target));

        foreach (var item in toInsert)
        {
            if (insertIndex > collection.Count)
            {
                insertIndex = collection.Count;
            }
            collection.Insert(insertIndex++, item);
        }
    }

    /// <summary>
    /// Link the provided items together with the target (if provided) into a single LinkedGroupId.
    /// If any item already belongs to a group, groups are unified.
    /// </summary>
    public async Task LinkItemsAsync(System.Collections.Generic.List<OrderItem> itemsToLink, OrderItem? target)
    {
        if (itemsToLink == null || itemsToLink.Count == 0) return;
        if (target == null) return; // need a target to link to

        // Collect existing group ids from items and target
        var existingGroupIds = itemsToLink.Select(i => i.LinkedGroupId).Where(g => g != null).ToList();
        if (target.LinkedGroupId != null) existingGroupIds.Add(target.LinkedGroupId);

        Guid groupId = existingGroupIds.FirstOrDefault() ?? Guid.NewGuid();

        // If there are multiple existing group ids, unify them by using groupId
        var allCandidates = Items.Concat(ArchivedItems).Where(i =>
            itemsToLink.Select(x => x.Id).Contains(i.Id) || i.Id == target.Id ||
            (i.LinkedGroupId != null && existingGroupIds.Contains(i.LinkedGroupId))).ToList();

        foreach (var it in allCandidates)
        {
            it.LinkedGroupId = groupId;
        }

        await SaveAsync();
        RefreshDisplayItems();
    }

    private void RefreshDisplayItems()
    {
        DisplayItems.Clear();

        // Build DisplayItems by iterating Items in their current order and
        // grouping linked members when first encountered. This preserves the
        // overall ordering of Items so reorders are reflected in the UI.
        var processed = new HashSet<Guid>();

        foreach (var item in Items)
        {
            if (processed.Contains(item.Id))
                continue;

            if (item.LinkedGroupId == null)
            {
                DisplayItems.Add(new OrderItemGroup(new[] { item }));
                processed.Add(item.Id);
            }
            else
            {
                var gid = item.LinkedGroupId;
                var members = Items.Where(i => i.LinkedGroupId == gid).ToList();
                if (members.Count > 0)
                {
                    foreach (var m in members)
                        processed.Add(m.Id);

                    DisplayItems.Add(new OrderItemGroup(members));
                }
            }
        }
    }

    public bool GetGroupState(string? name, bool defaultValue = true)
        => _groupStateStore.Get(name, defaultValue);

    public void SetGroupState(string? name, bool value)
        => _groupStateStore.Set(name, value);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _logger?.LogInformation("OrderLogViewModel disposed");
        }

        _disposed = true;
    }
}
