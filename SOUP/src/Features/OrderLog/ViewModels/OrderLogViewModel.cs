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
    private const int TimerIntervalSeconds = 1, DefaultUndoTimeoutSeconds = 5, StatusClearSeconds = 3;
    private const double DefaultCardFontSize = 13.0;

    private readonly IOrderLogService _orderLogService;
    private readonly GroupStateStore _groupStateStore;
    private readonly SettingsService _settingsService;
    private readonly DialogService _dialogService;
    private readonly ILogger<OrderLogViewModel>? _logger;
    private readonly OrderSearchService _searchService;
    private readonly OrderBulkOperationsService _bulkOperationsService;
    private readonly OrderLogClipboardService _clipboardService;
    private readonly VendorColorService _vendorColorService;
    private readonly UndoRedoStack _undoRedoStack;
    private readonly OrderGroupingService _groupingService;
    private readonly OrderTimestampSyncService _timestampSyncService;
    private readonly OrderLinkingService _linkingService;
    private readonly OrderArchiveService _archiveService;
    private readonly WorkTimeCalculationService _workTimeService;
    private readonly OrderCollectionManager _collectionManager;
    private readonly OrderDisplayRefreshCoordinator _displayRefreshCoordinator;
    private readonly OrderImportExportCoordinator _importExportCoordinator;
    private readonly OrderUndoCoordinator _undoCoordinator;
    
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    private DispatcherTimer? _statusClearTimer;
    private System.Threading.CancellationTokenSource? _saveDebounceCts;

    public ObservableCollection<OrderItem> Items { get; } = new();
    public ObservableCollection<OrderItem> ArchivedItems { get; } = new();
    public ObservableCollection<OrderItem> SelectedItems { get; } = new();

    [ObservableProperty]
    private int _selectedItemsCount;
    public ObservableCollection<OrderItem> StickyNotes { get; } = new();
    public ObservableCollection<OrderItemGroup> DisplayItems { get; } = new();
    public ObservableCollection<OrderItemGroup> DisplayArchivedItems { get; } = new();

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
    private bool _sortByStatus = true;
    [ObservableProperty]
    private bool _sortStatusDescending = false;
    [ObservableProperty]
    private bool _notReadyGroupExpanded = true;
    [ObservableProperty]
    private bool _onDeckGroupExpanded = true;
    [ObservableProperty]
    private bool _inProgressGroupExpanded = true;
    [ObservableProperty]
    private bool _notesExpanded = true;

    public ObservableCollection<OrderItemGroup> NotReadyItems { get; } = new();
    public ObservableCollection<OrderItemGroup> OnDeckItems { get; } = new();
    public ObservableCollection<OrderItemGroup> InProgressItems { get; } = new();

    [ObservableProperty]
    private int _notReadyCount;
    [ObservableProperty]
    private int _onDeckCount;
    [ObservableProperty]
    private int _inProgressCount;

    [ObservableProperty]
    private bool _notesOnlyMode = false;

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

    [ObservableProperty]
    private bool _isMultiSelectMode = false;
    [ObservableProperty]
    private bool _autoColorByVendor = true;

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

    partial void OnStatusFiltersChanged(OrderItem.OrderStatus[]? value) => RefreshDisplayItems();

    partial void OnFilterStartDateChanged(DateTime? value) => RefreshDisplayItems();

    partial void OnFilterEndDateChanged(DateTime? value) => RefreshDisplayItems();

    partial void OnColorFiltersChanged(string[]? value) => RefreshDisplayItems();

    partial void OnNoteTypeFilterChanged(NoteType? value) => RefreshDisplayItems();

    partial void OnNoteCategoryFilterChanged(NoteCategory? value) => RefreshDisplayItems();

    partial void OnAutoColorByVendorChanged(bool value) => SaveWidgetSettings();

    [ObservableProperty]
    private OrderItem? _selectedItem;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    partial void OnStatusMessageChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && !IsDefaultStatusMessage(value)) { _statusClearTimer ??= new() { Interval = TimeSpan.FromSeconds(StatusClearSeconds) }; _statusClearTimer.Stop(); _statusClearTimer.Tick -= OnStatusClearTimerTick; _statusClearTimer.Tick += OnStatusClearTimerTick; _statusClearTimer.Start(); }
    }

    private bool IsDefaultStatusMessage(string message) => message.Contains(" active") && message.Contains(" archived");

    private void UpdateDefaultStatus() => StatusMessage = $"{Items.Count} active · {ArchivedItems.Count} archived";

    private void OnStatusClearTimerTick(object? sender, EventArgs e) { _statusClearTimer?.Stop(); UpdateDefaultStatus(); }

    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    private bool _undoAvailable;
    [ObservableProperty]
    private string _undoMessage = string.Empty;
    [ObservableProperty]
    private int _undoSecondsRemaining;
    [ObservableProperty]
    private bool _redoAvailable;
    [ObservableProperty]
    private string _redoMessage = string.Empty;
    [ObservableProperty]
    private int _undoStackCount;
    [ObservableProperty]
    private int _redoStackCount;

    public IEnumerable<UndoableAction> UndoHistory => _undoRedoStack.UndoHistory;
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
    private string _stickyNoteColorHex = OrderLogColors.DefaultNote;

    public event Action? GroupStatesReset;
    public event Action<OrderItem>? ItemAdded;

    public OrderLogViewModel(IOrderLogService orderLogService, GroupStateStore groupStateStore, SettingsService settingsService, DialogService dialogService, OrderTimestampSyncService timestampSyncService, OrderLinkingService linkingService, OrderArchiveService archiveService, WorkTimeCalculationService workTimeService, OrderDisplayRefreshCoordinator displayRefreshCoordinator, OrderImportExportCoordinator importExportCoordinator, OrderUndoCoordinator undoCoordinator, ILogger<OrderLogViewModel>? logger = null)
    {
        (_orderLogService, _groupStateStore, _settingsService, _dialogService, _timestampSyncService, _linkingService, _archiveService, _workTimeService, _displayRefreshCoordinator, _importExportCoordinator, _undoCoordinator, _logger) = (orderLogService, groupStateStore, settingsService, dialogService, timestampSyncService, linkingService, archiveService, workTimeService, displayRefreshCoordinator, importExportCoordinator, undoCoordinator, logger);
        _collectionManager = new OrderCollectionManager(Items, ArchivedItems);
        (_searchService, _bulkOperationsService, _clipboardService, _vendorColorService, _undoRedoStack, _groupingService) = (new OrderSearchService(), new OrderBulkOperationsService(), new OrderLogClipboardService(null), new VendorColorService(null), new UndoRedoStack(maxHistorySize: 50), new OrderGroupingService());
        _timer = new() { Interval = TimeSpan.FromSeconds(TimerIntervalSeconds) }; _timer.Tick += OnTimerTick; _timer.Start();
        _undoRedoStack.StackChanged += OnUndoRedoStackChanged;
        _undoCoordinator.UndoTimerExpired += OnUndoTimerExpired; _undoCoordinator.SecondsRemainingChanged += seconds => UndoSecondsRemaining = seconds;
        DisplayItems.CollectionChanged += (s, e) => UpdateDisplayCounts(); DisplayArchivedItems.CollectionChanged += (s, e) => UpdateDisplayCounts();
        SelectedItems.CollectionChanged += (s, e) => SelectedItemsCount = SelectedItems.Count;
        UpdateDisplayCounts();
        _logger?.LogInformation("OrderLogViewModel initialized");
    }

    partial void OnNotesOnlyModeChanged(bool value) => SaveWidgetSettings();
    partial void OnNotesExpandedChanged(bool _) => SaveWidgetSettings();
    partial void OnNotReadyGroupExpandedChanged(bool _) => SaveWidgetSettings();
    partial void OnOnDeckGroupExpandedChanged(bool _) => SaveWidgetSettings();
    partial void OnInProgressGroupExpandedChanged(bool _) => SaveWidgetSettings();
    partial void OnUndoTimeoutSecondsChanged(int value) { _undoCoordinator.UpdateTimerInterval(value); SaveWidgetSettings(); }

    private void SaveWidgetSettings()
    {
        var settings = new OrderLogWidgetSettings { CardFontSize = CardFontSize, ShowNowPlaying = ShowNowPlaying, ShowArchived = ShowArchived, UndoTimeoutSeconds = UndoTimeoutSeconds, DefaultOrderColor = DefaultOrderColor, DefaultNoteColor = DefaultNoteColor, NotesOnlyMode = NotesOnlyMode, SortByStatus = SortByStatus,
            SortStatusDescending = SortStatusDescending,
            AutoColorByVendor = AutoColorByVendor,
            NotReadyGroupExpanded = NotReadyGroupExpanded,
            OnDeckGroupExpanded = OnDeckGroupExpanded,
            InProgressGroupExpanded = InProgressGroupExpanded,
            NotesExpanded = NotesExpanded
        };
        _ = _settingsService.SaveSettingsAsync("OrderLogWidget", settings);
    }

    private void OnTimerTick(object? sender, EventArgs e) { foreach (var item in Items) { item.RefreshTimeInProgress(); item.RefreshTimeOnDeck(); } }

    private void OnUndoRedoStackChanged()
    {
        (UndoAvailable, RedoAvailable, UndoStackCount, RedoStackCount) = (_undoRedoStack.CanUndo, _undoRedoStack.CanRedo, _undoRedoStack.UndoCount, _undoRedoStack.RedoCount);
        OnPropertyChanged(nameof(UndoHistory)); OnPropertyChanged(nameof(RedoHistory));
        UndoMessage = _undoRedoStack.CanUndo ? (_undoRedoStack.UndoHistory.FirstOrDefault() is var lastUndo && lastUndo != null ? $"Undo: {lastUndo.Description}" : "Undo available") : string.Empty;
        RedoMessage = _undoRedoStack.CanRedo ? (_undoRedoStack.RedoHistory.FirstOrDefault() is var lastRedo && lastRedo != null ? $"Redo: {lastRedo.Description}" : "Redo available") : string.Empty;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var s = await _settingsService.LoadSettingsAsync<OrderLogWidgetSettings>("OrderLogWidget");
            (CardFontSize, ShowNowPlaying, ShowArchived, UndoTimeoutSeconds, DefaultOrderColor, DefaultNoteColor, NotesOnlyMode, SortByStatus, SortStatusDescending, AutoColorByVendor, NotReadyGroupExpanded, OnDeckGroupExpanded, InProgressGroupExpanded, NotesExpanded) = (s.CardFontSize <= 0 ? DefaultCardFontSize : s.CardFontSize, s.ShowNowPlaying, s.ShowArchived, s.UndoTimeoutSeconds <= 0 ? DefaultUndoTimeoutSeconds : s.UndoTimeoutSeconds, string.IsNullOrEmpty(s.DefaultOrderColor) ? OrderLogColors.DefaultOrder : s.DefaultOrderColor, string.IsNullOrEmpty(s.DefaultNoteColor) ? OrderLogColors.DefaultNote : s.DefaultNoteColor, s.NotesOnlyMode, s.SortByStatus, s.SortStatusDescending, s.AutoColorByVendor, s.NotReadyGroupExpanded, s.OnDeckGroupExpanded, s.InProgressGroupExpanded, s.NotesExpanded);
        if (Application.Current != null) Application.Current.Resources["CardFontSize"] = CardFontSize;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load widget settings, using defaults");
        }

        await _vendorColorService.LoadMappingsAsync();
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            var all = await _orderLogService.LoadAsync();
            Items.Clear(); ArchivedItems.Clear(); _collectionManager.Clear();
            foreach (var it in all.OrderBy(i => i.Order)) { if (it.IsArchived) ArchivedItems.Add(it); else Items.Add(it); }
            _collectionManager.InitializeTracking();
            RefreshDisplayItems(); RefreshArchivedDisplayItems(); UpdateDefaultStatus();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to load order log items"); }
        finally { IsLoading = false; }
    }

    private async Task DebouncedSaveAsync(int debounceMs = 300)
    {
        try { _saveDebounceCts?.Cancel(); _saveDebounceCts = new System.Threading.CancellationTokenSource(); var token = _saveDebounceCts.Token; await Task.Delay(debounceMs, token); if (!token.IsCancellationRequested) await SaveAsync(); }
        catch (TaskCanceledException) { }
    }

    public async Task SaveAsync()
    {
        try { await _orderLogService.SaveAsync(Items.Concat(ArchivedItems).ToList()); }
        catch (Exception ex) { _logger?.LogError(ex, "Failed to save order log items"); }
    }

    [RelayCommand]
    public async Task ArchiveOrderAsync(OrderItem? item)
    {
        if (item == null || !Items.Any(i => i.Id == item.Id)) return;
        RemoveFromItems(item); AddToArchived(item); RefreshDisplayItems(); RefreshArchivedDisplayItems();
        await SaveAsync(); StatusMessage = "Archived item";
    }

    [RelayCommand]
    public async Task UnarchiveOrderAsync(OrderItem? item)
    {
        if (item == null || !ArchivedItems.Any(i => i.Id == item.Id)) return;
        RemoveFromArchived(item); AddToItems(item, insertAtTop: true); RefreshDisplayItems(); RefreshArchivedDisplayItems();
        await SaveAsync(); StatusMessage = "Unarchived item";
    }

    public void CycleSortMode()
    {
        SortModeEnum = SortModeEnum switch { OrderGroupingService.OrderLogSortMode.Status => OrderGroupingService.OrderLogSortMode.CreatedAt, OrderGroupingService.OrderLogSortMode.CreatedAt => OrderGroupingService.OrderLogSortMode.VendorName, _ => OrderGroupingService.OrderLogSortMode.Status };
        RefreshDisplayItems();
    }
    
    [RelayCommand]
    public async Task MoveUpAsync(OrderItem? item)
    {
        if (item == null) return;
        var idx = Items.IndexOf(item);
        if (idx > 0) { _undoRedoStack.ExecuteAction(new ReorderAction(item, Items as IList<OrderItem> ?? Items.ToList(), idx, idx - 1)); await DebouncedSaveAsync(); StatusMessage = "Moved up"; return; }
        var aidx = ArchivedItems.IndexOf(item);
        if (aidx > 0) { _undoRedoStack.ExecuteAction(new ReorderAction(item, ArchivedItems as IList<OrderItem> ?? ArchivedItems.ToList(), aidx, aidx - 1)); await DebouncedSaveAsync(); StatusMessage = "Moved up (archived)"; }
    }

    [RelayCommand]
    public async Task MoveDownAsync(OrderItem? item)
    {
        if (item == null) return;
        var idx = Items.IndexOf(item);
        if (idx >= 0 && idx < Items.Count - 1) { _undoRedoStack.ExecuteAction(new ReorderAction(item, Items as IList<OrderItem> ?? Items.ToList(), idx, idx + 1)); await DebouncedSaveAsync(); StatusMessage = "Moved down"; return; }
        var aidx = ArchivedItems.IndexOf(item);
        if (aidx >= 0 && aidx < ArchivedItems.Count - 1) { _undoRedoStack.ExecuteAction(new ReorderAction(item, ArchivedItems as IList<OrderItem> ?? ArchivedItems.ToList(), aidx, aidx + 1)); await DebouncedSaveAsync(); StatusMessage = "Moved down (archived)"; }
    }

    [RelayCommand]
    private async Task DeleteAsync(OrderItem? item)
    {
        if (item == null) return;
        RemoveFromItems(item); RemoveFromArchived(item); RefreshDisplayItems(); RefreshArchivedDisplayItems();
        await SaveAsync(); StatusMessage = "Deleted item";
    }

    private void StartUndoTimer(string message) => _undoCoordinator.StartUndoTimer(UndoTimeoutSeconds, message, msg => StatusMessage = msg);

    private void OnUndoTimerExpired() { UndoAvailable = false; UndoMessage = string.Empty; UpdateDefaultStatus(); }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (!_undoRedoStack.CanUndo) return;
        _undoRedoStack.Undo(); RefreshDisplayItems(); RefreshArchivedDisplayItems();
        await SaveAsync(); StatusMessage = "Undo applied";
    }

    [RelayCommand]
    private async Task RedoAsync()
    {
        if (!_undoRedoStack.CanRedo) return;
        _undoRedoStack.Redo(); RefreshDisplayItems(); RefreshArchivedDisplayItems();
        await SaveAsync(); StatusMessage = "Redo applied";
    }

    public async Task SetStatusAsync(OrderItem? item, OrderItem.OrderStatus status, OrderItem.OrderStatus? previousStatus = null)
    {
        if (item == null) return;
        var itemsToChange = item.LinkedGroupId != null ? AllItems.Where(i => i.LinkedGroupId == item.LinkedGroupId.Value).ToList() : new List<OrderItem> { item };
        var willBeArchived = status == OrderItem.OrderStatus.Done;
        if (willBeArchived) { _undoRedoStack.ExecuteAction(new ArchiveAction(itemsToChange)); foreach (var it in itemsToChange) { RemoveFromItems(it); AddToArchived(it); } }
        else { _undoRedoStack.ExecuteAction(new StatusChangeAction(itemsToChange, status)); foreach (var it in itemsToChange) it.Status = status; }
        if (itemsToChange.Count > 1) { var refItem = itemsToChange[0]; foreach (var it in itemsToChange.Skip(1)) it.SyncTimestampsFrom(refItem); foreach (var it in itemsToChange) { it.RefreshTimeInProgress(); it.RefreshTimeOnDeck(); } }
        RefreshDisplayItems();
        RefreshArchivedDisplayItems();
        await SaveAsync();
        StartUndoTimer(willBeArchived ? "Archived item" : "Changed status");
    }

    public async Task AddOrderAsync(OrderItem order)
    {
        AddToItems(order, insertAtTop: true); RefreshDisplayItems();
        await SaveAsync(); StatusMessage = "Order added"; ItemAdded?.Invoke(order);
    }

    public async Task<bool> AddOrderInlineAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteVendorName)) { StatusMessage = "Vendor name is required"; return false; }
        var colorToUse = AutoColorByVendor && !string.IsNullOrWhiteSpace(NewNoteVendorName) ? _vendorColorService.GetColorForVendor(NewNoteVendorName.Trim()) : _newNoteColorHex;
        var order = new OrderItem { VendorName = NewNoteVendorName.Trim(), TransferNumbers = NewNoteTransferNumbers?.Trim() ?? string.Empty, WhsShipmentNumbers = NewNoteWhsShipmentNumbers?.Trim() ?? string.Empty, ColorHex = colorToUse, CreatedAt = DateTime.UtcNow };
        AddToItems(order, insertAtTop: true);
        RefreshDisplayItems();
        await SaveAsync();
        NewNoteVendorName = string.Empty; NewNoteTransferNumbers = string.Empty; NewNoteWhsShipmentNumbers = string.Empty; _newNoteColorHex = DefaultOrderColor;
        StatusMessage = "Order added";
        ItemAdded?.Invoke(order);
        return true;
    }

    public async Task<bool> AddStickyNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(StickyNoteContent)) { StatusMessage = "Note content is required"; return false; }
        var note = new OrderItem { NoteType = NoteType.StickyNote, NoteTitle = string.Empty, NoteContent = StickyNoteContent.Trim(), ColorHex = _stickyNoteColorHex, CreatedAt = DateTime.UtcNow, Status = OrderItem.OrderStatus.OnDeck };
        AddToItems(note, insertAtTop: true);
        RefreshDisplayItems();
        await SaveAsync();
        StickyNoteContent = string.Empty; _stickyNoteColorHex = DefaultNoteColor;
        StatusMessage = "Sticky note added";
        ItemAdded?.Invoke(note);
        return true;
    }

    public async Task AddQuickStickyNoteAsync(string content, string? colorHex = null)
    {
        var note = new OrderItem { NoteType = NoteType.StickyNote, NoteTitle = string.Empty, NoteContent = content.Trim(), ColorHex = colorHex ?? DefaultNoteColor, CreatedAt = DateTime.UtcNow, Status = OrderItem.OrderStatus.OnDeck };
        AddToItems(note, insertAtTop: true);
        RefreshDisplayItems();
        await SaveAsync();
        StatusMessage = "Quick note added";
        ItemAdded?.Invoke(note);
    }

    public void SetStickyNoteColor(string colorHex) => _stickyNoteColorHex = colorHex;

    public string GetStickyNoteColor() => _stickyNoteColorHex;

    public void SetNewNoteColor(string colorHex) => _newNoteColorHex = colorHex;

    public string GetNewNoteColor() => _newNoteColorHex;

    [RelayCommand]
    private void ToggleArchived() => ShowArchived = !ShowArchived;

    [RelayCommand]
    private void ClearSearch() => SearchQuery = string.Empty;

    [RelayCommand]
    private void ClearFilters() { SearchQuery = string.Empty; StatusFilters = null; FilterStartDate = null; FilterEndDate = null; ColorFilters = null; NoteTypeFilter = null; NoteCategoryFilter = null; IsSearchActive = false; StatusMessage = "Filters cleared"; }

    // Bulk Operations Commands

    [RelayCommand]
    private void ToggleMultiSelectMode() { IsMultiSelectMode = !IsMultiSelectMode; if (!IsMultiSelectMode) SelectedItems.Clear(); StatusMessage = IsMultiSelectMode ? "Multi-select mode enabled" : "Multi-select mode disabled"; }

    [RelayCommand]
    private void ClearSelection() { SelectedItems.Clear(); StatusMessage = "Selection cleared"; }

    [RelayCommand]
    private async Task BulkArchiveAsync()
    {
        if (SelectedItems.Count == 0) { StatusMessage = "No items selected"; return; }
        var itemsToArchive = SelectedItems.ToList();
        _undoRedoStack.ExecuteAction(new ArchiveAction(itemsToArchive));
        foreach (var item in itemsToArchive) { RemoveFromItems(item); AddToArchived(item); }
        SelectedItems.Clear();
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Archived {itemsToArchive.Count} item(s)");
        StatusMessage = $"Archived {itemsToArchive.Count} item(s)";
    }

    [RelayCommand]
    private async Task BulkUnarchiveAsync()
    {
        if (SelectedItems.Count == 0) { StatusMessage = "No items selected"; return; }
        var itemsToUnarchive = SelectedItems.ToList();
        _undoRedoStack.ExecuteAction(new UnarchiveAction(itemsToUnarchive));
        foreach (var item in itemsToUnarchive) { RemoveFromArchived(item); AddToItems(item, insertAtTop: true); }
        SelectedItems.Clear();
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Unarchived {itemsToUnarchive.Count} item(s)");
        StatusMessage = $"Unarchived {itemsToUnarchive.Count} item(s)";
    }

    [RelayCommand]
    private async Task BulkDeleteAsync()
    {
        if (SelectedItems.Count == 0) { StatusMessage = "No items selected"; return; }
        if (MessageBox.Show($"Are you sure you want to delete {SelectedItems.Count} selected item(s)?", "Delete Items", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        { StatusMessage = "Delete cancelled"; return; }
        var itemsToDelete = SelectedItems.ToList();
        var collection = itemsToDelete.Any(i => Items.Contains(i)) ? Items : ArchivedItems;
        _undoRedoStack.ExecuteAction(new DeleteAction(itemsToDelete, collection));
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
        if (SelectedItems.Count == 0) { StatusMessage = "No items selected"; return; }
        var itemsToUpdate = SelectedItems.Where(i => !i.IsArchived).ToList();
        if (itemsToUpdate.Count == 0) { StatusMessage = "No active items selected"; return; }
        if (newStatus == OrderItem.OrderStatus.Done) { _undoRedoStack.ExecuteAction(new ArchiveAction(itemsToUpdate)); foreach (var item in itemsToUpdate) { RemoveFromItems(item); AddToArchived(item); } }
        else _undoRedoStack.ExecuteAction(new StatusChangeAction(itemsToUpdate, newStatus));
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
        if (SelectedItems.Count == 0) { StatusMessage = "No items selected"; return; }
        var stickyNotes = SelectedItems.Where(i => i.NoteType == NoteType.StickyNote).ToList();
        if (stickyNotes.Count == 0) { StatusMessage = "No sticky notes selected (color only applies to sticky notes)"; return; }
        _undoRedoStack.ExecuteAction(new ColorChangeAction(stickyNotes, colorHex));
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Updated color for {stickyNotes.Count} sticky note(s)");
        StatusMessage = $"Updated color for {stickyNotes.Count} sticky note(s)";
    }

    [RelayCommand]
    private async Task BulkLinkAsync()
    {
        if (SelectedItems.Count < 2) { StatusMessage = "Select at least 2 items to link"; return; }
        var itemsToLink = SelectedItems.ToList();
        _undoRedoStack.ExecuteAction(new LinkAction(itemsToLink, Guid.NewGuid()));
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Linked {itemsToLink.Count} item(s)");
        StatusMessage = $"Linked {itemsToLink.Count} item(s)";
    }

    [RelayCommand]
    private async Task BulkUnlinkAsync()
    {
        if (SelectedItems.Count == 0) { StatusMessage = "No items selected"; return; }
        var itemsToUnlink = SelectedItems.Where(i => i.LinkedGroupId != null).ToList();
        if (itemsToUnlink.Count == 0) { StatusMessage = "No linked items selected"; return; }
        _undoRedoStack.ExecuteAction(new UnlinkAction(itemsToUnlink));
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Unlinked {itemsToUnlink.Count} item(s)");
        StatusMessage = $"Unlinked {itemsToUnlink.Count} item(s)";
    }

    [RelayCommand]
    private void NavigateToItem(OrderItem? item)
    {
        if (item == null) return;
        CurrentNavigationItem = item;
        for (int i = 0; i < DisplayItems.Count; i++) { if (DisplayItems[i].Members.Contains(item)) { CurrentItemIndex = i; break; } }
    }

    [RelayCommand]
    private void NavigateNext() => NavigateToIndex(CurrentItemIndex + 1, wrapAround: true);

    [RelayCommand]
    private void NavigatePrevious() => NavigateToIndex(CurrentItemIndex - 1, wrapAround: true);

    [RelayCommand]
    private void NavigateToTop() => NavigateToIndex(0);

    [RelayCommand]
    private void NavigateToBottom() => NavigateToIndex(DisplayItems.Count - 1);

    private void NavigateToIndex(int targetIndex, bool wrapAround = false)
    {
        if (DisplayItems.Count == 0) return;
        if (wrapAround) { if (targetIndex < 0) targetIndex = DisplayItems.Count - 1; if (targetIndex >= DisplayItems.Count) targetIndex = 0; }
        else targetIndex = Math.Clamp(targetIndex, 0, DisplayItems.Count - 1);
        CurrentItemIndex = targetIndex;
        CurrentNavigationItem = DisplayItems[targetIndex].First;
    }

    [RelayCommand]
    private async Task ExportToCsv()
    {
        var result = await _importExportCoordinator.ExportToCsvAsync(ShowArchived ? ArchivedItems : Items, loading => IsLoading = loading, status => StatusMessage = status);
        if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage)) StatusMessage = result.ErrorMessage;
    }

    [RelayCommand]
    private async Task ExportToJson()
    {
        var result = await _importExportCoordinator.ExportToJsonAsync(ShowArchived ? ArchivedItems : Items, loading => IsLoading = loading, status => StatusMessage = status);
        if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage)) StatusMessage = result.ErrorMessage;
    }

    [RelayCommand]
    private async Task ImportFromCsv()
    {
        var result = await _importExportCoordinator.ImportFromCsvAsync(loading => IsLoading = loading, status => StatusMessage = status);
        if (result.Success && result.ItemCount > 0) await LoadAsync();
        else if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage)) StatusMessage = result.ErrorMessage;
    }

    [RelayCommand]
    private void Copy()
    {
        var itemsToCopy = SelectedItems.Count > 0 ? SelectedItems.ToList() : (SelectedItem != null ? new List<OrderItem> { SelectedItem } : new List<OrderItem>());
        if (itemsToCopy.Count == 0) { StatusMessage = "No items selected to copy"; return; }
        _clipboardService.CopyToClipboard(itemsToCopy);
        StatusMessage = $"Copied {itemsToCopy.Count} item(s) to clipboard";
        _logger?.LogInformation("Copied {Count} items to clipboard", itemsToCopy.Count);
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (!_clipboardService.TryPasteFromClipboard(out var pastedItems)) { StatusMessage = "Clipboard does not contain valid order data"; return; }
        if (pastedItems.Count == 0) { StatusMessage = "No items to paste"; return; }
        if (AutoColorByVendor) foreach (var item in pastedItems.Where(i => i.NoteType == NoteType.Order && !string.IsNullOrWhiteSpace(i.VendorName))) item.ColorHex = _vendorColorService.GetColorForVendor(item.VendorName!);
        int insertIndex = SelectedItem != null && Items.Contains(SelectedItem) ? Items.IndexOf(SelectedItem) + 1 : 0;
        _undoRedoStack.ExecuteAction(new PasteAction(pastedItems, Items, insertIndex));
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Pasted {pastedItems.Count} item(s)");
        StatusMessage = $"Pasted {pastedItems.Count} item(s)";
        _logger?.LogInformation("Pasted {Count} items", pastedItems.Count);
    }

    [RelayCommand]
    private async Task DuplicateAsync()
    {
        var itemsToDuplicate = SelectedItems.Count > 0 ? SelectedItems.ToList() : (SelectedItem != null ? new List<OrderItem> { SelectedItem } : new List<OrderItem>());
        if (itemsToDuplicate.Count == 0) { StatusMessage = "No items selected to duplicate"; return; }
        var duplicatedItems = _clipboardService.CloneItems(itemsToDuplicate);
        if (AutoColorByVendor) foreach (var item in duplicatedItems.Where(i => i.NoteType == NoteType.Order && !string.IsNullOrWhiteSpace(i.VendorName))) item.ColorHex = _vendorColorService.GetColorForVendor(item.VendorName!);
        int insertIndex = SelectedItem != null && Items.Contains(SelectedItem) ? Items.IndexOf(SelectedItem) + 1 : 0;
        _undoRedoStack.ExecuteAction(new PasteAction(duplicatedItems, Items, insertIndex));
        RefreshDisplayItems();
        await SaveAsync();
        StartUndoTimer($"Duplicated {duplicatedItems.Count} item(s)");
        StatusMessage = $"Duplicated {duplicatedItems.Count} item(s)";
        _logger?.LogInformation("Duplicated {Count} items", duplicatedItems.Count);
    }

    

    public async Task MoveOrderAsync(OrderItem dropped, OrderItem? target)
    {
        if (dropped == target) return;
        if (target == null) { if (Items.Contains(dropped)) { Items.Remove(dropped); Items.Add(dropped); } }
        else { int oldIndex = Items.IndexOf(dropped); int newIndex = Items.IndexOf(target); if (oldIndex < 0 || newIndex < 0) return; Items.RemoveAt(oldIndex); if (oldIndex < newIndex) newIndex--; Items.Insert(newIndex, dropped); }
        await SaveAsync();
    }

    public void SwapOrders(OrderItem item1, OrderItem item2)
    {
        if (item1 == null || item2 == null || item1.Id == item2.Id) return;
        var collection = Items.Contains(item1) ? Items : ArchivedItems;
        var idx1 = collection.IndexOf(item1);
        var idx2 = collection.IndexOf(item2);
        if (idx1 < 0 || idx2 < 0) return;
        collection.Move(idx1, idx2);
        RefreshDisplayItems();
    }

    public void MoveItemToIndex(OrderItem item, int newIndex)
    {
        if (item == null) return;
        var collection = Items.Contains(item) ? Items : ArchivedItems;
        var currentIndex = collection.IndexOf(item);
        if (currentIndex < 0 || currentIndex == newIndex || newIndex < 0 || newIndex >= collection.Count) return;
        collection.Move(currentIndex, newIndex);
        RefreshDisplayItems();
    }

    public async Task MoveOrdersAsync(System.Collections.Generic.List<OrderItem> droppedItems, OrderItem? target)
    {
        if (droppedItems == null || droppedItems.Count == 0) return;
        if (droppedItems.Count == 1 && droppedItems[0].LinkedGroupId != null) { var groupMembers = AllItems.Where(i => i.LinkedGroupId == droppedItems[0].LinkedGroupId).ToList(); if (groupMembers.Count > 1) droppedItems = groupMembers; }
        bool operateOnItems = droppedItems.Any(d => Items.Contains(d));
        if (target != null) operateOnItems = Items.Contains(target);
        MoveItemsInCollection(droppedItems, target, operateOnItems ? Items : ArchivedItems);
        await SaveAsync();
        RefreshDisplayItems();
    }

    private void MoveItemsInCollection(List<OrderItem> droppedItems, OrderItem? target, ObservableCollection<OrderItem> collection)
    {
        var toInsert = droppedItems.Where(d => collection.Contains(d)).OrderBy(d => collection.IndexOf(d)).ToList();
        foreach (var item in toInsert) collection.Remove(item);
        int insertIndex = target == null ? collection.Count : Math.Max(0, collection.IndexOf(target));
        foreach (var item in toInsert) { if (insertIndex > collection.Count) insertIndex = collection.Count; collection.Insert(insertIndex++, item); }
    }

    public async Task LinkItemsAsync(System.Collections.Generic.List<OrderItem> itemsToLink, OrderItem? target)
    {
        var result = _linkingService.LinkItems(itemsToLink, target, Items);
        if (result.Success) { await SaveAsync(); RefreshDisplayItems(); }
    }

    public void RefreshDisplayItems()
    {
        _displayRefreshCoordinator.RefreshDisplayCollection(Items, DisplayItems, SortByStatus, SortStatusDescending, SortModeEnum, SearchQuery, StatusFilters, FilterStartDate, FilterEndDate, ColorFilters, NoteTypeFilter, NoteCategoryFilter);
        _displayRefreshCoordinator.RefreshStatusGroups(Items, NotReadyItems, OnDeckItems, InProgressItems, SearchQuery, StatusFilters, FilterStartDate, FilterEndDate, ColorFilters, NoteTypeFilter, NoteCategoryFilter);
        _displayRefreshCoordinator.RefreshStickyNotes(Items, StickyNotes, SearchQuery, StatusFilters, FilterStartDate, FilterEndDate, ColorFilters, NoteTypeFilter, NoteCategoryFilter);
        _displayRefreshCoordinator.UpdateLinkedItemCounts(_collectionManager.AllItems);
        NotReadyCount = NotReadyItems.Count; OnDeckCount = OnDeckItems.Count; InProgressCount = InProgressItems.Count;
        UpdateDisplayCounts();
    }

    public void RefreshArchivedDisplayItems() => _displayRefreshCoordinator.RefreshArchivedDisplayItems(ArchivedItems, DisplayArchivedItems, SearchQuery, StatusFilters, FilterStartDate, FilterEndDate, ColorFilters, NoteTypeFilter, NoteCategoryFilter, () => UpdateDisplayCounts());

    public async Task RefreshArchivedDisplayItemsAsync() => await Task.Run(() => RefreshArchivedDisplayItems());

    private void UpdateDisplayCounts() { DisplayItemsCount = DisplayItems.Sum(g => g.Members.Count); DisplayArchivedItemsCount = DisplayArchivedItems.Sum(g => g.Members.Count); DisplayMembersCount = Items.Count + ArchivedItems.Count; }

    public bool GetGroupState(string? name, bool defaultValue = true) => _groupStateStore.Get(name, defaultValue);
    public void SetGroupState(string? name, bool value) => _groupStateStore.Set(name, value);

    private void AddToItems(OrderItem item, bool insertAtTop = false) => _collectionManager.AddToItems(item, insertAtTop);
    private void RemoveFromItems(OrderItem item) => _collectionManager.RemoveFromItems(item);
    private void AddToArchived(OrderItem item) => _collectionManager.AddToArchived(item);
    private void RemoveFromArchived(OrderItem item) => _collectionManager.RemoveFromArchived(item);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop(); _timer.Tick -= OnTimerTick;
        _undoCoordinator.Dispose(); _collectionManager.Clear();
        _logger?.LogInformation("OrderLogViewModel disposed");
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    public async Task ClearAllArchivedAsync()
    {
        if (ArchivedItems.Count == 0) { StatusMessage = "No archived items to clear"; return; }
        if (MessageBox.Show("Are you sure you want to permanently delete all archived items?", "Clear Archived", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { StatusMessage = "Clear archived cancelled"; return; }
        foreach (var it in ArchivedItems.ToList()) RemoveFromArchived(it);
        RefreshArchivedDisplayItems(); await SaveAsync(); StatusMessage = "Cleared archived items";
    }
}
