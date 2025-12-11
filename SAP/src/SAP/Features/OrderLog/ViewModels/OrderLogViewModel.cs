using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SAP.Features.OrderLog.Models;
using SAP.Features.OrderLog.Services;

namespace SAP.Features.OrderLog.ViewModels;

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

    private string _newNoteColorHex = "#B56576";

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
        _newNoteColorHex = "#B56576";

        StatusMessage = "Order added";
        return true;
    }

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

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"OrderLog_Export_{timestamp}.csv";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            var itemsToExport = ShowArchived ? ArchivedItems.ToList() : Items.ToList();
            
            var csvContent = new System.Text.StringBuilder();
            csvContent.AppendLine("Vendor,Status,Created,Completed,Transfer Numbers,WHS Shipment Numbers");
            
            foreach (var item in itemsToExport)
            {
                var vendor = EscapeCsvField(item.VendorName);
                var status = item.Status.ToString();
                var created = item.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                var completed = item.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
                var transfers = EscapeCsvField(item.TransferNumbers ?? "");
                var shipments = EscapeCsvField(item.WhsShipmentNumbers ?? "");
                
                csvContent.AppendLine($"{vendor},{status},{created},{completed},{transfers},{shipments}");
            }
            
            await System.IO.File.WriteAllTextAsync(filePath, csvContent.ToString());
            StatusMessage = $"Exported {itemsToExport.Count} orders to {fileName}";
            _logger?.LogInformation("Exported {Count} orders to CSV: {FilePath}", itemsToExport.Count, filePath);
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

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
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
