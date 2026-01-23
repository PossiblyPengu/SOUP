using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SOUP.Features.OrderLog.Constants;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.Services;
using SOUP.Infrastructure.Services;
using SOUP.Services;

namespace SOUP.Features.OrderLog.ViewModels;

public partial class OrderLogViewModel : ObservableObject, IDisposable
{
    // Constants for configurable timeouts
    private const int TimerIntervalSeconds = 1;
    private const int DefaultUndoTimeoutSeconds = 5;
    private const int StatusClearSeconds = 3;
    private const double DefaultCardFontSize = 13.0;

    private readonly IOrderLogService _orderLogService;
    private readonly GroupStateStore _groupStateStore;
    private readonly SettingsService _settingsService;
    private readonly DialogService _dialogService;
    private readonly ILogger<OrderLogViewModel>? _logger;
    private readonly OrderSearchService _searchService;
    private readonly OrderBulkOperationsService _bulkOperationsService;
    private readonly UndoRedoStack _undoRedoStack;
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    private DispatcherTimer? _undoTimer;
    private DispatcherTimer? _statusClearTimer;
    private System.Threading.CancellationTokenSource? _saveDebounceCts;

    // Lock for thread-safe access to HashSets
    private readonly Lock _collectionLock = new();

    // HashSets for O(1) membership checks instead of O(n) Contains on ObservableCollection
    private readonly HashSet<Guid> _itemIds = new();
    private readonly HashSet<Guid> _archivedItemIds = new();

    public ObservableCollection<OrderItem> Items { get; } = new();
    public ObservableCollection<OrderItem> ArchivedItems { get; } = new();
    public ObservableCollection<OrderItem> SelectedItems { get; } = new();
    public ObservableCollection<OrderItem> StickyNotes { get; } = new(); // Dedicated sticky notes collection
    public ObservableCollection<OrderItemGroup> DisplayItems { get; } = new();
    public ObservableCollection<OrderItemGroup> DisplayArchivedItems { get; } = new();

    // Grouping helper service (extracted to simplify VM)
    private readonly OrderGroupingService _groupingService;

    public OrderGroupingService.OrderLogSortMode SortModeEnum { get; private set; } = OrderGroupingService.OrderLogSortMode.Status;

    [ObservableProperty]
    private int _displayItemsCount;

    [ObservableProperty]
    private int _displayArchivedItemsCount;

    [ObservableProperty]
    private int _displayMembersCount;

    /// <summary>Helper to get all items (active + archived) without repeated Concat calls</summary>
    private IEnumerable<OrderItem> AllItems => Items.Concat(ArchivedItems);

    [ObservableProperty]
    private bool _showArchived = false;

    /// <summary>
    /// Gets or sets whether the widget is in notes-only mode (hides order functionality).
    /// </summary>
    [ObservableProperty]
    private bool _notesOnlyMode = false;

    // Search & Filter Properties
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearchActive = false;

    [ObservableProperty]
    private OrderItem.OrderStatus[]? _statusFilters = null;

    [ObservableProperty]
    private DateTime? _filterStartDate = null;

    [ObservableProperty]
    private DateTime? _filterEndDate = null;

    [ObservableProperty]
    private string[]? _colorFilters = null;

    [ObservableProperty]
    private NoteType? _noteTypeFilter = null;

    [ObservableProperty]
    private NoteCategory? _noteCategoryFilter = null;

    // Multi-select mode for bulk operations
    [ObservableProperty]
    private bool _isMultiSelectMode = false;

    // Navigation properties for enhanced navigation
    [ObservableProperty]
    private OrderItem? _currentNavigationItem = null;

    [ObservableProperty]
    private int _currentItemIndex = -1;

    [ObservableProperty]
    private double _savedScrollPosition = 0;

    partial void OnSearchQueryChanged(string value)
    {
        IsSearchActive = !string.IsNullOrWhiteSpace(value);
        RefreshDisplayItems();
    }

    partial void OnStatusFiltersChanged(OrderItem.OrderStatus[]? value)
    {
        RefreshDisplayItems();
    }

    partial void OnFilterStartDateChanged(DateTime? value)
    {
        RefreshDisplayItems();
    }

    partial void OnFilterEndDateChanged(DateTime? value)
    {
        RefreshDisplayItems();
    }

    partial void OnColorFiltersChanged(string[]? value)
    {
        RefreshDisplayItems();
    }

    partial void OnNoteTypeFilterChanged(NoteType? value)
    {
        RefreshDisplayItems();
    }

    partial void OnNoteCategoryFilterChanged(NoteCategory? value)
    {
        RefreshDisplayItems();
    }

    [ObservableProperty]
    private OrderItem? _selectedItem;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    partial void OnStatusMessageChanged(string value)
    {
        // Auto-clear status message after timeout (unless it's empty or the default count message)
        if (!string.IsNullOrEmpty(value) && !IsDefaultStatusMessage(value))
        {
            _statusClearTimer ??= new() { Interval = TimeSpan.FromSeconds(StatusClearSeconds) };
            _statusClearTimer.Stop();
            _statusClearTimer.Tick -= OnStatusClearTimerTick;
            _statusClearTimer.Tick += OnStatusClearTimerTick;
            _statusClearTimer.Start();
        }
    }

    private bool IsDefaultStatusMessage(string message)
    {
        // Default status shows counts like "5 active · 3 archived"
        return message.Contains(" active") && message.Contains(" archived");
    }

    private void UpdateDefaultStatus()
    {
        StatusMessage = $"{Items.Count} active · {ArchivedItems.Count} archived";
    }

    private void OnStatusClearTimerTick(object? sender, EventArgs e)
    {
        _statusClearTimer?.Stop();
        UpdateDefaultStatus();
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _undoAvailable;

    [ObservableProperty]
    private string _undoMessage = string.Empty;

    [ObservableProperty]
    private bool _redoAvailable;

    [ObservableProperty]
    private string _redoMessage = string.Empty;

    [ObservableProperty]
    private int _undoStackCount;

    [ObservableProperty]
    private int _redoStackCount;

    /// <summary>
    /// Gets the undo history for display in UI
    /// </summary>
    public IEnumerable<UndoableAction> UndoHistory => _undoRedoStack.UndoHistory;

    /// <summary>
    /// Gets the redo history for display in UI
    /// </summary>
    public IEnumerable<UndoableAction> RedoHistory => _undoRedoStack.RedoHistory;

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
    public event Action<OrderItem>? ItemAdded;

    public OrderLogViewModel(
        IOrderLogService orderLogService,
        GroupStateStore groupStateStore,
        SettingsService settingsService,
        DialogService dialogService,
        ILogger<OrderLogViewModel>? logger = null)
    {
        _orderLogService = orderLogService;
        _groupStateStore = groupStateStore;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _logger = logger;
        _searchService = new OrderSearchService();
        _bulkOperationsService = new OrderBulkOperationsService();
        _undoRedoStack = new UndoRedoStack(maxHistorySize: 50);

        _timer = new() { Interval = TimeSpan.FromSeconds(TimerIntervalSeconds) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        // Wire up undo/redo stack changes to update UI
        _undoRedoStack.StackChanged += OnUndoRedoStackChanged;

        // grouping helper service (extracted to simplify VM)
        _groupingService = new OrderGroupingService();

        // Ensure counts update when display collections change
        DisplayItems.CollectionChanged += (s, e) => UpdateDisplayCounts();
        DisplayArchivedItems.CollectionChanged += (s, e) => UpdateDisplayCounts();

        UpdateDisplayCounts();

        _logger?.LogInformation("OrderLogViewModel initialized");
    }

    [ObservableProperty]
    private double _cardFontSize = DefaultCardFontSize;

    [ObservableProperty]
    private bool _showNowPlaying = true;

    [ObservableProperty]
    private int _undoTimeoutSeconds = DefaultUndoTimeoutSeconds;

    [ObservableProperty]
    private string _defaultOrderColor = OrderLogColors.DefaultOrder;

    [ObservableProperty]
    private string _defaultNoteColor = OrderLogColors.DefaultNote;

    [ObservableProperty]
    private bool _sortByStatus = true; // Always true - status headers always visible

    [ObservableProperty]
    private bool _sortStatusDescending = false;

    partial void OnSortStatusDescendingChanged(bool value)
    {
        RefreshDisplayItems();
    }

    public void CycleSortMode()
    {
        SortModeEnum = SortModeEnum switch
        {
            OrderGroupingService.OrderLogSortMode.Status => OrderGroupingService.OrderLogSortMode.CreatedAt,
            OrderGroupingService.OrderLogSortMode.CreatedAt => OrderGroupingService.OrderLogSortMode.VendorName,
            _ => OrderGroupingService.OrderLogSortMode.Status
        };
        RefreshDisplayItems();
    }

    // Status group expand/collapse state
    [ObservableProperty]
    private bool _notReadyGroupExpanded = true;

    [ObservableProperty]
    private bool _onDeckGroupExpanded = true;

    [ObservableProperty]
    private bool _inProgressGroupExpanded = true;

    // Status-grouped items for collapsible view
    public ObservableCollection<OrderItemGroup> NotReadyItems { get; } = new();
    public ObservableCollection<OrderItemGroup> OnDeckItems { get; } = new();
    public ObservableCollection<OrderItemGroup> InProgressItems { get; } = new();

    // Count properties for status groups (ObservableCollection.Count doesn't raise PropertyChanged)
    [ObservableProperty]
    private int _notReadyCount;

    [ObservableProperty]
    private int _onDeckCount;

    [ObservableProperty]
    private int _inProgressCount;

    partial void OnCardFontSizeChanged(double value)
    {
        try
        {
            if (Application.Current != null)
            {
                // Update app-level resources
                Application.Current.Resources["CardFontSize"] = value;

                // Also update any open OrderLogWidgetWindow instances
                foreach (var window in Application.Current.Windows.OfType<SOUP.Windows.OrderLogWidgetWindow>())
                {
                    window.Resources["CardFontSize"] = value;
                }
            }

            // persist
            SaveWidgetSettings();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update card font size");
        }
    }

    partial void OnShowNowPlayingChanged(bool value)
    {
        SaveWidgetSettings();
    }

    partial void OnShowArchivedChanged(bool value)
    {
        SaveWidgetSettings();
    }

    partial void OnUndoTimeoutSecondsChanged(int value)
    {
        SaveWidgetSettings();
    }

    partial void OnDefaultOrderColorChanged(string value)
    {
        SaveWidgetSettings();
    }

    partial void OnDefaultNoteColorChanged(string value)
    {
        SaveWidgetSettings();
    }

    partial void OnSortByStatusChanged(bool value)
    {
        RefreshDisplayItems();
    }

    partial void OnNotesOnlyModeChanged(bool value)
    {
        SaveWidgetSettings();
    }

    partial void OnNotReadyGroupExpandedChanged(bool value)
    {
        SaveWidgetSettings();
    }

    partial void OnOnDeckGroupExpandedChanged(bool value)
    {
        SaveWidgetSettings();
    }

    partial void OnInProgressGroupExpandedChanged(bool value)
    {
        SaveWidgetSettings();
    }

    private void SaveWidgetSettings()
    {
        var settings = new OrderLogWidgetSettings
        {
            CardFontSize = CardFontSize,
            ShowNowPlaying = ShowNowPlaying,
            ShowArchived = ShowArchived,
            UndoTimeoutSeconds = UndoTimeoutSeconds,
            DefaultOrderColor = DefaultOrderColor,
            DefaultNoteColor = DefaultNoteColor,
            NotesOnlyMode = NotesOnlyMode,
            SortByStatus = SortByStatus,
            SortStatusDescending = SortStatusDescending,
            NotReadyGroupExpanded = NotReadyGroupExpanded,
            OnDeckGroupExpanded = OnDeckGroupExpanded,
            InProgressGroupExpanded = InProgressGroupExpanded
        };
        _ = _settingsService.SaveSettingsAsync("OrderLogWidget", settings);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshTimeInProgress();
        }
    }

    private void OnUndoRedoStackChanged()
    {
        UndoAvailable = _undoRedoStack.CanUndo;
        RedoAvailable = _undoRedoStack.CanRedo;
        UndoStackCount = _undoRedoStack.UndoCount;
        RedoStackCount = _undoRedoStack.RedoCount;

        // Notify property changes for history collections
        OnPropertyChanged(nameof(UndoHistory));
        OnPropertyChanged(nameof(RedoHistory));

        // Update messages
        if (_undoRedoStack.CanUndo)
        {
            var lastAction = _undoRedoStack.UndoHistory.FirstOrDefault();
            UndoMessage = lastAction != null ? $"Undo: {lastAction.Description}" : "Undo available";
        }
        else
        {
            UndoMessage = string.Empty;
        }

        if (_undoRedoStack.CanRedo)
        {
            var lastAction = _undoRedoStack.RedoHistory.FirstOrDefault();
            RedoMessage = lastAction != null ? $"Redo: {lastAction.Description}" : "Redo available";
        }
        else
        {
            RedoMessage = string.Empty;
        }
    }

    public async Task InitializeAsync()
    {
        // load persisted widget settings
        try
        {
            var s = await _settingsService.LoadSettingsAsync<OrderLogWidgetSettings>("OrderLogWidget");
            CardFontSize = s.CardFontSize <= 0 ? DefaultCardFontSize : s.CardFontSize;
            // Note: ShowNowPlaying defaults to true in the model, so we can use it directly
            ShowNowPlaying = s.ShowNowPlaying;
            ShowArchived = s.ShowArchived;
            UndoTimeoutSeconds = s.UndoTimeoutSeconds <= 0 ? DefaultUndoTimeoutSeconds : s.UndoTimeoutSeconds;
            DefaultOrderColor = string.IsNullOrEmpty(s.DefaultOrderColor) ? OrderLogColors.DefaultOrder : s.DefaultOrderColor;
            DefaultNoteColor = string.IsNullOrEmpty(s.DefaultNoteColor) ? OrderLogColors.DefaultNote : s.DefaultNoteColor;
            NotesOnlyMode = s.NotesOnlyMode;
            // Sorting preferences
            SortByStatus = s.SortByStatus;
            SortStatusDescending = s.SortStatusDescending;
            // Status group expand/collapse state
            NotReadyGroupExpanded = s.NotReadyGroupExpanded;
            OnDeckGroupExpanded = s.OnDeckGroupExpanded;
            InProgressGroupExpanded = s.InProgressGroupExpanded;
            if (Application.Current != null) Application.Current.Resources["CardFontSize"] = CardFontSize;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load widget settings, using defaults");
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Capture dispatcher BEFORE any await - this ensures we post back to the calling thread
        var dispatcher = Dispatcher.CurrentDispatcher;

        IsLoading = true;
        StatusMessage = "Loading orders...";

        try
        {
            var items = await _orderLogService.LoadAsync();

            // Must update collections on UI thread that owns them
            await dispatcher.InvokeAsync(() =>
            {
                Items.Clear();
                ArchivedItems.Clear();
                _itemIds.Clear();
                _archivedItemIds.Clear();

                foreach (var item in items)
                {
                    if (item.IsArchived)
                    {
                        ArchivedItems.Add(item);
                        _archivedItemIds.Add(item.Id);
                    }
                    else
                    {
                        Items.Add(item);
                        _itemIds.Add(item.Id);
                    }
                }

                RefreshDisplayItems();
                RefreshArchivedDisplayItems();
            });

            UpdateDefaultStatus();
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
            // Snapshot collections to avoid race conditions
            var itemsSnapshot = Items.ToList();
            var archivedSnapshot = ArchivedItems.ToList();

            for (int i = 0; i < itemsSnapshot.Count; i++)
            {
                itemsSnapshot[i].Order = i;
            }

            for (int i = 0; i < archivedSnapshot.Count; i++)
            {
                archivedSnapshot[i].Order = itemsSnapshot.Count + i;
            }

            var allItems = itemsSnapshot.Concat(archivedSnapshot).ToList();
            await _orderLogService.SaveAsync(allItems);
            StatusMessage = $"Saved {Items.Count} orders ({ArchivedItems.Count} archived)";
            RefreshDisplayItems();
            RefreshArchivedDisplayItems();
            _logger?.LogInformation("Saved {Count} orders", allItems.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            _logger?.LogError(ex, "Failed to save orders");
        }
    }

    /// <summary>
    /// Debounced save - waits 300ms for additional changes before saving.
    /// Use for rapid operations like move up/down, status changes, etc.
    /// </summary>
    private async Task DebouncedSaveAsync()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new();

        try
        {
            await Task.Delay(300, _saveDebounceCts.Token);
            await SaveAsync();
        }
        catch (TaskCanceledException)
        {
            // Expected when another save is triggered within debounce window
        }
    }

    [RelayCommand]
    public async Task ArchiveOrderAsync(OrderItem? item)
    {
        if (item == null) return;

        var action = new ArchiveAction(new[] { item });
        _undoRedoStack.ExecuteAction(action);

        RemoveFromItems(item);
        AddToArchived(item);
        await SaveAsync();

        StartUndoTimer("Archived 1 item");
        StatusMessage = "Order archived - tap Undo to restore";
    }

    /// <summary>
    /// Archive multiple items at once with undo support
    /// </summary>
    public async Task ArchiveItemsAsync(IEnumerable<OrderItem> items)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return;

        var action = new ArchiveAction(itemList);
        _undoRedoStack.ExecuteAction(action);

        foreach (var item in itemList)
        {
            RemoveFromItems(item);
            AddToArchived(item);
        }

        await SaveAsync();
        StartUndoTimer($"Archived {itemList.Count} item(s)");
        StatusMessage = $"Archived {itemList.Count} item(s) - tap Undo to restore";
    }

    [RelayCommand]
    public async Task UnarchiveOrderAsync(OrderItem? item)
    {
        if (item == null) return;

        // Prevent duplication if already in Items (O(1) check)
        if (_itemIds.Contains(item.Id)) return;

        var action = new UnarchiveAction(new[] { item });
        _undoRedoStack.ExecuteAction(action);

        RemoveFromArchived(item);
        AddToItems(item, insertAtTop: true);
        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();

        StartUndoTimer("Restored 1 item");
        StatusMessage = "Order restored - tap Undo to re-archive";
    }

    /// <summary>
    /// Restore multiple items from archive with undo support
    /// </summary>
    public async Task UnarchiveItemsAsync(IEnumerable<OrderItem> items)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return;

        var itemsToUnarchive = new List<OrderItem>();
        foreach (var item in itemList)
        {
            // O(1) membership check
            if (_itemIds.Contains(item.Id)) continue;
            itemsToUnarchive.Add(item);
        }

        if (itemsToUnarchive.Count == 0) return;

        var action = new UnarchiveAction(itemsToUnarchive);
        _undoRedoStack.ExecuteAction(action);

        foreach (var item in itemsToUnarchive)
        {
            RemoveFromArchived(item);
            AddToItems(item, insertAtTop: true);
        }

        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Restored {itemsToUnarchive.Count} item(s)");
        StatusMessage = $"Restored {itemsToUnarchive.Count} item(s) - tap Undo to re-archive";
    }

    /// <summary>
    /// Permanently delete all archived items
    /// </summary>
    public async Task ClearAllArchivedAsync()
    {
        if (ArchivedItems.Count == 0) return;

        var toRemove = ArchivedItems.ToList();
        foreach (var item in toRemove)
        {
            RemoveFromArchived(item);
        }

        RefreshArchivedDisplayItems();
        await SaveAsync();

        _logger?.LogInformation("Cleared {Count} archived items", toRemove.Count);
    }


    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        var count = SelectedItems.Count;
        var toRemove = SelectedItems.ToList();
        foreach (var item in toRemove)
        {
            RemoveFromItems(item);
            RemoveFromArchived(item);
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
            var action = new ReorderAction(item, Items as IList<OrderItem> ?? Items.ToList(), idx, idx - 1);
            _undoRedoStack.ExecuteAction(action);
            await DebouncedSaveAsync();
            StatusMessage = "Moved up";
            return;
        }

        // Try archived list
        var aidx = ArchivedItems.IndexOf(item);
        if (aidx > 0)
        {
            var action = new ReorderAction(item, ArchivedItems as IList<OrderItem> ?? ArchivedItems.ToList(), aidx, aidx - 1);
            _undoRedoStack.ExecuteAction(action);
            await DebouncedSaveAsync();
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
            var action = new ReorderAction(item, Items as IList<OrderItem> ?? Items.ToList(), idx, idx + 1);
            _undoRedoStack.ExecuteAction(action);
            await DebouncedSaveAsync();
            StatusMessage = "Moved down";
            return;
        }

        // archived items
        var aidx = ArchivedItems.IndexOf(item);
        if (aidx >= 0 && aidx < ArchivedItems.Count - 1)
        {
            var action = new ReorderAction(item, ArchivedItems as IList<OrderItem> ?? ArchivedItems.ToList(), aidx, aidx + 1);
            _undoRedoStack.ExecuteAction(action);
            await DebouncedSaveAsync();
            StatusMessage = "Moved down (archived)";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(OrderItem? item)
    {
        if (item == null) return;

        RemoveFromItems(item);
        RemoveFromArchived(item);
        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();
        StatusMessage = "Deleted item";
    }

    private void StartUndoTimer(string message)
    {
        StatusMessage = message + " - tap Undo to revert";

        // Reuse existing timer instead of creating new one each time
        if (_undoTimer == null)
        {
            _undoTimer = new() { Interval = TimeSpan.FromSeconds(UndoTimeoutSeconds) };
            _undoTimer.Tick += OnUndoTimerTick;
        }
        else
        {
            _undoTimer.Stop();
        }
        _undoTimer.Start();
    }

    private void OnUndoTimerTick(object? sender, EventArgs e)
    {
        // Timer expired - no action needed, stack remains available
        _undoTimer?.Stop();
    }

    /// <summary>
    /// Undo the last action
    /// </summary>
    [RelayCommand]
    private async Task UndoAsync()
    {
        if (!_undoRedoStack.CanUndo) return;

        _undoRedoStack.Undo();

        // Sync UI with model changes
        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();

        StatusMessage = "Undo applied";
    }

    /// <summary>
    /// Redo the last undone action
    /// </summary>
    [RelayCommand]
    private async Task RedoAsync()
    {
        if (!_undoRedoStack.CanRedo) return;

        _undoRedoStack.Redo();

        // Sync UI with model changes
        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();

        StatusMessage = "Redo applied";
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
        await SetStatusAsync(item, OrderItem.OrderStatus.Done);
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
        var oldColor = item.ColorHex;
        var action = new ColorChangeAction(new[] { item }, colorHex);
        _undoRedoStack.ExecuteAction(action);
        await SaveAsync();
        StartUndoTimer("Changed item color");
    }

    /// <summary>
    /// Helper method to edit a field with undo support
    /// </summary>
    public async Task EditFieldAsync(OrderItem item, string fieldName, object? oldValue, object? newValue, Action<object?> setter)
    {
        var action = new FieldEditAction(item, fieldName, oldValue, newValue, setter);
        _undoRedoStack.ExecuteAction(action);
        await SaveAsync();
        StartUndoTimer($"Edited {fieldName}");
    }

    public async Task SetStatusAsync(OrderItem item, OrderItem.OrderStatus status, OrderItem.OrderStatus? previousStatus = null)
    {
        if (item == null) return;

        _logger?.LogDebug("SetStatusAsync called for Item={ItemId} Status={Status} PreviousStatus={PreviousStatus} LinkedGroupId={LinkedGroupId}",
            item.Id, status, previousStatus, item.LinkedGroupId);

        // If this item is part of a linked group, apply the status change to all members
        if (item.LinkedGroupId != null)
        {
            var gid = item.LinkedGroupId.Value;
            var members = AllItems.Where(i => i.LinkedGroupId == gid).ToList();

            _logger?.LogDebug("Group {GroupId} members: {MemberIds}", gid, string.Join(',', members.Select(m => m.Id)));

            var shouldBeArchived = status == OrderItem.OrderStatus.Done;

            // Create appropriate action based on whether we're archiving or changing status
            if (shouldBeArchived)
            {
                var action = new ArchiveAction(members);
                _undoRedoStack.ExecuteAction(action);
            }
            else
            {
                var action = new StatusChangeAction(members, status);
                _undoRedoStack.ExecuteAction(action);
            }

            // Move members between collections based on IsArchived flag
            foreach (var m in members)
            {
                _logger?.LogDebug("Member {MemberId} pre-sync IsArchived={IsArchived} inItems={InItems} inArchived={InArchived}",
                    m.Id, m.IsArchived, _itemIds.Contains(m.Id), _archivedItemIds.Contains(m.Id));

                if (m.IsArchived)
                {
                    RemoveFromItems(m);
                    AddToArchived(m);
                }
                else
                {
                    RemoveFromArchived(m);
                    AddToItems(m, insertAtTop: true);
                }

                _logger?.LogDebug("Member {MemberId} post-sync IsArchived={IsArchived} inItems={InItems} inArchived={InArchived}",
                    m.Id, m.IsArchived, _itemIds.Contains(m.Id), _archivedItemIds.Contains(m.Id));
            }

            RefreshDisplayItems();
            RefreshArchivedDisplayItems();
            await SaveAsync();

            StartUndoTimer(shouldBeArchived ? $"Archived {members.Count} item(s)" : $"Changed status of {members.Count} item(s)");
            return;
        }

        // Note: item.Status may already be updated by TwoWay binding, so we need to
        // determine archival based on whether status IS Done, not was/will-be.
        var willBeArchived = status == OrderItem.OrderStatus.Done;
        var isCurrentlyInItems = _itemIds.Contains(item.Id);
        var isCurrentlyInArchived = _archivedItemIds.Contains(item.Id);

        // Create appropriate action
        if (willBeArchived && isCurrentlyInItems)
        {
            // Moving to Done - archive it
            var action = new ArchiveAction(new[] { item });
            _undoRedoStack.ExecuteAction(action);
            RemoveFromItems(item);
            AddToArchived(item);
        }
        else if (!willBeArchived && isCurrentlyInArchived)
        {
            // Moving away from Done - unarchive it
            var action = new UnarchiveAction(new[] { item });
            _undoRedoStack.ExecuteAction(action);
            RemoveFromArchived(item);
            AddToItems(item, insertAtTop: true);
        }
        else
        {
            // Just changing status, not archiving/unarchiving
            var action = new StatusChangeAction(new[] { item }, status);
            _undoRedoStack.ExecuteAction(action);
        }

        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();

        StartUndoTimer(willBeArchived ? "Archived item" : "Changed status");
    }

    public async Task AddOrderAsync(OrderItem order)
    {
        AddToItems(order, insertAtTop: true);
        RefreshDisplayItems();
        await SaveAsync();
        StatusMessage = "Order added";
        ItemAdded?.Invoke(order);
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

        AddToItems(order, insertAtTop: true);
        RefreshDisplayItems();
        await SaveAsync();

        // Clear the form
        NewNoteVendorName = string.Empty;
        NewNoteTransferNumbers = string.Empty;
        NewNoteWhsShipmentNumbers = string.Empty;
        _newNoteColorHex = DefaultOrderColor;

        StatusMessage = "Order added";
        ItemAdded?.Invoke(order);
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

        AddToItems(note, insertAtTop: true);
        RefreshDisplayItems();
        await SaveAsync();

        // Clear the form
        StickyNoteContent = string.Empty;
        _stickyNoteColorHex = DefaultNoteColor;

        StatusMessage = "Sticky note added";
        ItemAdded?.Invoke(note);
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
            ColorHex = colorHex ?? DefaultNoteColor,
            CreatedAt = DateTime.UtcNow,
            Status = OrderItem.OrderStatus.OnDeck
        };

        AddToItems(note, insertAtTop: true);
        RefreshDisplayItems();
        await SaveAsync();
        StatusMessage = "Quick note added";
        ItemAdded?.Invoke(note);
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
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        IsSearchActive = false;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        StatusFilters = null;
        FilterStartDate = null;
        FilterEndDate = null;
        ColorFilters = null;
        NoteTypeFilter = null;
        NoteCategoryFilter = null;
        IsSearchActive = false;
        StatusMessage = "Filters cleared";
    }

    // Bulk Operations Commands

    [RelayCommand]
    private void ToggleMultiSelectMode()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode)
        {
            SelectedItems.Clear();
        }
        StatusMessage = IsMultiSelectMode ? "Multi-select mode enabled" : "Multi-select mode disabled";
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedItems.Clear();
        StatusMessage = "Selection cleared";
    }

    [RelayCommand]
    private async Task BulkArchiveAsync()
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = "No items selected";
            return;
        }

        var itemsToArchive = SelectedItems.ToList();
        var action = new ArchiveAction(itemsToArchive);
        _undoRedoStack.ExecuteAction(action);

        // Move items from Items to ArchivedItems
        foreach (var item in itemsToArchive)
        {
            RemoveFromItems(item);
            AddToArchived(item);
        }

        SelectedItems.Clear();
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Archived {itemsToArchive.Count} item(s)");
        StatusMessage = $"Archived {itemsToArchive.Count} item(s)";
    }

    [RelayCommand]
    private async Task BulkUnarchiveAsync()
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = "No items selected";
            return;
        }

        var itemsToUnarchive = SelectedItems.ToList();
        var action = new UnarchiveAction(itemsToUnarchive);
        _undoRedoStack.ExecuteAction(action);

        // Move items from ArchivedItems to Items
        foreach (var item in itemsToUnarchive)
        {
            RemoveFromArchived(item);
            AddToItems(item, insertAtTop: true);
        }

        SelectedItems.Clear();
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Unarchived {itemsToUnarchive.Count} item(s)");
        StatusMessage = $"Unarchived {itemsToUnarchive.Count} item(s)";
    }

    [RelayCommand]
    private async Task BulkDeleteAsync()
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = "No items selected";
            return;
        }

        var count = SelectedItems.Count;
        var confirmResult = MessageBox.Show(
            $"Are you sure you want to delete {count} selected item(s)?",
            "Delete Items",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
        {
            StatusMessage = "Delete cancelled";
            return;
        }

        var itemsToDelete = SelectedItems.ToList();

        // Determine which collection to use for delete action
        var collection = itemsToDelete.Any(i => _itemIds.Contains(i.Id)) ? Items : ArchivedItems;
        var action = new DeleteAction(itemsToDelete, collection);
        _undoRedoStack.ExecuteAction(action);

        SelectedItems.Clear();
        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();

        StartUndoTimer($"Deleted {itemsToDelete.Count} item(s)");
        StatusMessage = $"Deleted {itemsToDelete.Count} item(s)";
    }

    [RelayCommand]
    private async Task BulkSetStatusAsync(OrderItem.OrderStatus newStatus)
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = "No items selected";
            return;
        }

        var itemsToUpdate = SelectedItems.Where(i => !i.IsArchived).ToList();
        if (itemsToUpdate.Count == 0)
        {
            StatusMessage = "No active items selected";
            return;
        }

        // If setting to Done, archive the items
        if (newStatus == OrderItem.OrderStatus.Done)
        {
            var action = new ArchiveAction(itemsToUpdate);
            _undoRedoStack.ExecuteAction(action);

            foreach (var item in itemsToUpdate)
            {
                RemoveFromItems(item);
                AddToArchived(item);
            }
        }
        else
        {
            var action = new StatusChangeAction(itemsToUpdate, newStatus);
            _undoRedoStack.ExecuteAction(action);
        }

        SelectedItems.Clear();
        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();

        StartUndoTimer($"Updated {itemsToUpdate.Count} item(s) to {newStatus}");
        StatusMessage = $"Updated {itemsToUpdate.Count} item(s) to {newStatus}";
    }

    [RelayCommand]
    private async Task BulkSetColorAsync(string colorHex)
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = "No items selected";
            return;
        }

        var stickyNotes = SelectedItems.Where(i => i.NoteType == NoteType.StickyNote).ToList();
        if (stickyNotes.Count == 0)
        {
            StatusMessage = "No sticky notes selected (color only applies to sticky notes)";
            return;
        }

        var action = new ColorChangeAction(stickyNotes, colorHex);
        _undoRedoStack.ExecuteAction(action);

        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Updated color for {stickyNotes.Count} sticky note(s)");
        StatusMessage = $"Updated color for {stickyNotes.Count} sticky note(s)";
    }

    [RelayCommand]
    private async Task BulkLinkAsync()
    {
        if (SelectedItems.Count < 2)
        {
            StatusMessage = "Select at least 2 items to link";
            return;
        }

        var itemsToLink = SelectedItems.ToList();
        var groupId = Guid.NewGuid();
        var action = new LinkAction(itemsToLink, groupId);
        _undoRedoStack.ExecuteAction(action);

        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Linked {itemsToLink.Count} item(s)");
        StatusMessage = $"Linked {itemsToLink.Count} item(s)";
    }

    [RelayCommand]
    private async Task BulkUnlinkAsync()
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = "No items selected";
            return;
        }

        var itemsToUnlink = SelectedItems.Where(i => i.LinkedGroupId != null).ToList();
        if (itemsToUnlink.Count == 0)
        {
            StatusMessage = "No linked items selected";
            return;
        }

        var action = new UnlinkAction(itemsToUnlink);
        _undoRedoStack.ExecuteAction(action);

        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Unlinked {itemsToUnlink.Count} item(s)");
        StatusMessage = $"Unlinked {itemsToUnlink.Count} item(s)";
    }

    // Navigation Commands

    [RelayCommand]
    private void NavigateToItem(OrderItem? item)
    {
        if (item == null) return;
        CurrentNavigationItem = item;

        // Find the index in DisplayItems
        for (int i = 0; i < DisplayItems.Count; i++)
        {
            if (DisplayItems[i].Members.Contains(item))
            {
                CurrentItemIndex = i;
                break;
            }
        }
    }

    [RelayCommand]
    private void NavigateNext()
    {
        if (DisplayItems.Count == 0) return;

        if (CurrentItemIndex < DisplayItems.Count - 1)
        {
            CurrentItemIndex++;
            var nextGroup = DisplayItems[CurrentItemIndex];
            CurrentNavigationItem = nextGroup.First;
        }
        else
        {
            // Wrap around to the beginning
            CurrentItemIndex = 0;
            CurrentNavigationItem = DisplayItems[0].First;
        }
    }

    [RelayCommand]
    private void NavigatePrevious()
    {
        if (DisplayItems.Count == 0) return;

        if (CurrentItemIndex > 0)
        {
            CurrentItemIndex--;
            var prevGroup = DisplayItems[CurrentItemIndex];
            CurrentNavigationItem = prevGroup.First;
        }
        else
        {
            // Wrap around to the end
            CurrentItemIndex = DisplayItems.Count - 1;
            CurrentNavigationItem = DisplayItems[CurrentItemIndex].First;
        }
    }

    [RelayCommand]
    private void NavigateToTop()
    {
        if (DisplayItems.Count == 0) return;

        CurrentItemIndex = 0;
        CurrentNavigationItem = DisplayItems[0].First;
    }

    [RelayCommand]
    private void NavigateToBottom()
    {
        if (DisplayItems.Count == 0) return;

        CurrentItemIndex = DisplayItems.Count - 1;
        CurrentNavigationItem = DisplayItems[CurrentItemIndex].First;
    }

    /// <summary>
    /// Saves the current scroll position for persistence
    /// </summary>
    public void SaveScrollPosition(double position)
    {
        SavedScrollPosition = position;
    }

    /// <summary>
    /// Restores the saved scroll position
    /// </summary>
    public double GetSavedScrollPosition()
    {
        return SavedScrollPosition;
    }

    [RelayCommand]
    private async Task ExportToCsv()
    {
        try
        {
            var itemsToExport = ShowArchived ? ArchivedItems : Items;
            if (itemsToExport.Count == 0)
            {
                StatusMessage = "No items to export";
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"OrderLog_Export_{timestamp}.csv";

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Export to CSV",
                defaultFileName,
                "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");

            if (string.IsNullOrEmpty(filePath))
            {
                StatusMessage = "Export cancelled";
                return;
            }

            IsLoading = true;
            StatusMessage = "Exporting to CSV...";

            var exportService = new Services.OrderLogExportService();
            await exportService.ExportToCsvAsync(itemsToExport, filePath);

            var fileName = System.IO.Path.GetFileName(filePath);
            StatusMessage = $"Exported {itemsToExport.Count} item(s)";
            _dialogService.ShowExportSuccessDialog(fileName, filePath, itemsToExport.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _logger?.LogError(ex, "Failed to export to CSV");
            _dialogService.ShowExportErrorDialog(ex.Message);
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
            if (_itemIds.Contains(dropped.Id))
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
    /// Swap two orders' positions in the collection. Used for iOS-style slide-past reordering.
    /// </summary>
    public void SwapOrders(OrderItem item1, OrderItem item2)
    {
        if (item1 == null || item2 == null || item1.Id == item2.Id) return;

        // Find which collection contains the items
        var collection = _itemIds.Contains(item1.Id) ? Items : ArchivedItems;

        var idx1 = collection.IndexOf(item1);
        var idx2 = collection.IndexOf(item2);

        if (idx1 < 0 || idx2 < 0) return;

        // Swap by moving
        collection.Move(idx1, idx2);

        // Don't save yet - we'll save when drag finishes to avoid excessive I/O
        RefreshDisplayItems();
    }

    /// <summary>
    /// Move an item to a specific index in its collection. Used after drag-based reordering.
    /// </summary>
    public void MoveItemToIndex(OrderItem item, int newIndex)
    {
        if (item == null) return;

        var collection = _itemIds.Contains(item.Id) ? Items : ArchivedItems;
        var currentIndex = collection.IndexOf(item);

        if (currentIndex < 0 || currentIndex == newIndex) return;
        if (newIndex < 0 || newIndex >= collection.Count) return;

        collection.Move(currentIndex, newIndex);
        RefreshDisplayItems();
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
            var groupMembers = AllItems.Where(i => i.LinkedGroupId == gid).ToList();
            if (groupMembers.Count > 1)
            {
                droppedItems = groupMembers;
            }
        }

        // Determine target collection based on where items/target are located (O(1) checks)
        bool operateOnItems = droppedItems.Any(d => _itemIds.Contains(d.Id));
        if (target != null)
        {
            operateOnItems = _itemIds.Contains(target.Id);
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
        // Simple, deterministic linking algorithm
        if (itemsToLink == null || itemsToLink.Count == 0) return;

        // Ensure target exists and is renderable
        if (target == null) return;
        if (!target.IsRenderable) return;

        // Only link active (non-archived) items - resolve by ID to get the actual instances from Items
        var activeItemsById = Items.ToDictionary(i => i.Id);

        // Resolve target from Items collection
        if (!activeItemsById.TryGetValue(target.Id, out var actualTarget))
        {
            // Target is not in active items - don't link to archived items
            return;
        }
        target = actualTarget;

        // Resolve candidates from Items collection only
        var candidates = new List<OrderItem>();
        foreach (var it in itemsToLink)
        {
            if (it == null) continue;
            if (!activeItemsById.TryGetValue(it.Id, out var knownItem)) continue;
            if (!knownItem.IsRenderable) continue;
            candidates.Add(knownItem);
        }
        if (candidates.Count == 0) return;

        // Enforce same NoteType as target
        candidates = candidates.Where(i => i.NoteType == target.NoteType).ToList();
        if (candidates.Count == 0) return;

        // Determine group id: prefer target's group, otherwise any candidate's group, otherwise new
        Guid groupId;
        if (target.LinkedGroupId != null) groupId = target.LinkedGroupId.Value;
        else
        {
            var existing = candidates.Select(c => c.LinkedGroupId).FirstOrDefault(g => g != null);
            groupId = existing ?? Guid.NewGuid();
        }

        // Assign group id to target
        target.LinkedGroupId = groupId;

        // Assign group id to candidates
        foreach (var c in candidates)
            c.LinkedGroupId = groupId;

        // Also pull in any other active items that already belonged to these groups (to unify groups)
        var groupsToUnify = new HashSet<Guid>(candidates.Select(c => c.LinkedGroupId ?? Guid.Empty).Where(g => g != Guid.Empty));
        if (target.LinkedGroupId != null) groupsToUnify.Add(target.LinkedGroupId.Value);

        foreach (var it in Items)
        {
            if (it.LinkedGroupId != null && groupsToUnify.Contains(it.LinkedGroupId.Value))
            {
                if (it.IsRenderable)
                    it.LinkedGroupId = groupId;
            }
        }

        await SaveAsync();
        RefreshDisplayItems();
    }

    /// <summary>
    /// Refresh the display items from the Items collection.
    /// Call this after modifying Items externally (e.g., linking orders).
    /// </summary>
    public void RefreshDisplayItems()
    {
        RefreshDisplayCollection(Items, DisplayItems);
        RefreshStatusGroups();
        RefreshStickyNotes();
        UpdateLinkedItemCounts();
    }

    /// <summary>
    /// Refresh the sticky notes collection (separate from orders).
    /// </summary>
    private void RefreshStickyNotes()
    {
        StickyNotes.Clear();
        var notes = Items.Where(i => i.IsStickyNote).OrderBy(i => i.CreatedAt);
        foreach (var note in notes)
        {
            StickyNotes.Add(note);
        }
    }

    /// <summary>
    /// Update the LinkedItemCount property for all items based on their LinkedGroupId.
    /// </summary>
    private void UpdateLinkedItemCounts()
    {
        // Group all items by their LinkedGroupId
        var linkedGroups = AllItems
            .Where(i => i.LinkedGroupId != null)
            .GroupBy(i => i.LinkedGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Update each item's count (count - 1 to exclude itself)
        foreach (var item in AllItems)
        {
            if (item.LinkedGroupId != null && linkedGroups.TryGetValue(item.LinkedGroupId.Value, out var count))
            {
                item.LinkedItemCount = count - 1; // Exclude the item itself
            }
            else
            {
                item.LinkedItemCount = 0;
            }
        }
    }

    /// <summary>
    /// Refresh the status-grouped collections for collapsible status view.
    /// </summary>
    private void RefreshStatusGroups()
    {
        // Delegate status-group population to the grouping service
        _groupingService.PopulateStatusGroups(Items, NotReadyItems, OnDeckItems, InProgressItems);

        // Update count properties (ObservableCollection.Count doesn't raise PropertyChanged)
        NotReadyCount = NotReadyItems.Count;
        OnDeckCount = OnDeckItems.Count;
        InProgressCount = InProgressItems.Count;
    }

    /// <summary>
    /// Refresh the archived display items from the ArchivedItems collection.
    /// </summary>
    public void RefreshArchivedDisplayItems() => RefreshDisplayCollection(ArchivedItems, DisplayArchivedItems);

    /// <summary>
    /// Shared helper to build grouped display items from a source collection.
    /// Groups linked items together and optionally sorts groups by status.
    /// Sticky notes are excluded from sorting and appended in original order.
    /// Applies search/filter before grouping.
    /// </summary>
    private void RefreshDisplayCollection(
        ObservableCollection<OrderItem> source,
        ObservableCollection<OrderItemGroup> display)
    {
        // Apply search and filters first
        IEnumerable<OrderItem> filtered = source;

        if (_searchService.HasActiveFilters(SearchQuery, StatusFilters, FilterStartDate, FilterEndDate, ColorFilters, NoteTypeFilter, NoteCategoryFilter))
        {
            filtered = _searchService.ApplyAllFilters(
                source,
                SearchQuery,
                StatusFilters,
                FilterStartDate,
                FilterEndDate,
                ColorFilters,
                NoteTypeFilter,
                NoteCategoryFilter);
        }

        // Convert to ObservableCollection for grouping service
        var filteredCollection = new ObservableCollection<OrderItem>(filtered);

        // Use grouping service to build ordered display collection and apply it
        var built = _groupingService.BuildDisplayCollection(filteredCollection, SortByStatus, SortStatusDescending, SortModeEnum);
        display.Clear();
        foreach (var g in built)
        {
            display.Add(g);
        }

        // Log grouping details for diagnostics
        try
        {
            var details = string.Join(',', built.Select(g => $"{(g.LinkedGroupId?.ToString() ?? "(null)")}:{g.Count}"));
            _logger?.LogInformation("OrderLog grouping built {GroupCount} groups: {Details}", built.Count, details);
        }
        catch { }

        UpdateDisplayCounts();
    }

    private void UpdateDisplayCounts()
    {
        // DisplayItems/DisplayArchivedItems are groups; DisplayMembersCount counts total members
        DisplayItemsCount = DisplayItems.Sum(g => g.Members.Count);
        DisplayArchivedItemsCount = DisplayArchivedItems.Sum(g => g.Members.Count);
        DisplayMembersCount = Items.Count + ArchivedItems.Count;
    }

    public bool GetGroupState(string? name, bool defaultValue = true)
        => _groupStateStore.Get(name, defaultValue);

    public void SetGroupState(string? name, bool value)
        => _groupStateStore.Set(name, value);

    #region Collection Helper Methods (O(1) membership with HashSet tracking)

    /// <summary>
    /// Adds an item to the Items collection with O(1) membership tracking.
    /// </summary>
    private void AddToItems(OrderItem item, bool insertAtTop = false)
    {
        lock (_collectionLock)
        {
            if (_itemIds.Contains(item.Id)) return;

            if (insertAtTop)
                Items.Insert(0, item);
            else
                Items.Add(item);
            _itemIds.Add(item.Id);
        }
    }

    /// <summary>
    /// Removes an item from the Items collection with O(1) membership tracking.
    /// </summary>
    private void RemoveFromItems(OrderItem item)
    {
        lock (_collectionLock)
        {
            if (!_itemIds.Contains(item.Id)) return;

            Items.Remove(item);
            _itemIds.Remove(item.Id);
        }
    }

    /// <summary>
    /// Adds an item to the ArchivedItems collection with O(1) membership tracking.
    /// </summary>
    private void AddToArchived(OrderItem item)
    {
        lock (_collectionLock)
        {
            if (_archivedItemIds.Contains(item.Id)) return;

            ArchivedItems.Add(item);
            _archivedItemIds.Add(item.Id);
        }
    }

    /// <summary>
    /// Removes an item from the ArchivedItems collection with O(1) membership tracking.
    /// </summary>
    private void RemoveFromArchived(OrderItem item)
    {
        lock (_collectionLock)
        {
            if (!_archivedItemIds.Contains(item.Id)) return;

            ArchivedItems.Remove(item);
            _archivedItemIds.Remove(item.Id);
        }
    }

    #endregion

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

            if (_undoTimer != null)
            {
                _undoTimer.Stop();
                _undoTimer.Tick -= OnUndoTimerTick;
            }

            _itemIds.Clear();
            _archivedItemIds.Clear();

            _logger?.LogInformation("OrderLogViewModel disposed");
        }

        _disposed = true;
    }
}
