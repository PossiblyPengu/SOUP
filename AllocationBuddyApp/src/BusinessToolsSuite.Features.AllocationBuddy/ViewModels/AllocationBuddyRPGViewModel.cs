using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BusinessToolsSuite.Core.Entities.AllocationBuddy;
using BusinessToolsSuite.Infrastructure.Services.Parsers;
using BusinessToolsSuite.Features.AllocationBuddy.Helpers;
using BusinessToolsSuite.Shared.Services;
using BusinessToolsSuite.Core.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;

namespace BusinessToolsSuite.Features.AllocationBuddy.ViewModels;

public partial class AllocationBuddyRPGViewModel : ObservableObject
{
    private readonly AllocationBuddyParser _parser;
    private readonly DialogService _dialogService;
    private readonly ILogger<AllocationBuddyRPGViewModel>? _logger;

    public ObservableCollection<LocationAllocation> LocationAllocations { get; } = new();
    public ObservableCollection<ItemAllocation> ItemPool { get; } = new();
    public ObservableCollection<FileImportResult> FileImportResults { get; } = new();
    
    // Undo buffer for last deactivation
    private DeactivationRecord? _lastDeactivation;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public int TotalEntries => LocationAllocations.Sum(l => l.Items.Sum(i => i.Quantity));

    public int LocationsCount => LocationAllocations.Count;

    public int ItemPoolCount => ItemPool.Count;

    public string FooterMessage => _statusMessage;

    public IRelayCommand ImportCommand { get; }
    public IAsyncRelayCommand<string[]> ImportFilesCommand { get; }
    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand RemoveOneCommand { get; }
    public IRelayCommand AddOneCommand { get; }
    public IRelayCommand MoveFromPoolCommand { get; }
    public IAsyncRelayCommand<LocationAllocation> DeactivateStoreCommand { get; }
    public IRelayCommand UndoDeactivateCommand { get; }

    public AllocationBuddyRPGViewModel(AllocationBuddyParser parser, DialogService dialogService, ILogger<AllocationBuddyRPGViewModel>? logger = null)
    {
        _parser = parser;
        _dialogService = dialogService;
        _logger = logger;

        ImportCommand = new RelayCommand(async () => await ImportAsync());
        ImportFilesCommand = new AsyncRelayCommand<string[]>(ImportFilesAsync);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        ClearCommand = new RelayCommand(ClearSearch);
        RemoveOneCommand = new RelayCommand<ItemAllocation>(RemoveOne);
        AddOneCommand = new RelayCommand<ItemAllocation>(AddOne);
        MoveFromPoolCommand = new RelayCommand<ItemAllocation>(MoveFromPool);
        DeactivateStoreCommand = new AsyncRelayCommand<LocationAllocation>(DeactivateStoreAsync);
        UndoDeactivateCommand = new RelayCommand(UndoDeactivate, () => _lastDeactivation != null);

        // Load dictionary into parser if available
        try
        {
            var dictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "UnifiedApp", "src", "renderer", "modules", "allocation-buddy", "src", "js", "dictionaries.js");
            var dict = DictionaryLoader.LoadFromJs(dictPath);
            _parser.SetDictionaryItems(dict.Select(d => new BusinessToolsSuite.Infrastructure.Services.Parsers.DictionaryItem { Number = d.Number, Description = d.Description, Skus = d.Skus }).ToList());
        }
        catch { }
    }

    private async Task DeactivateStoreAsync(LocationAllocation loc)
    {
        if (loc == null) return;

        // Confirm first (await the dialog instead of blocking)
        var confirm = new BusinessToolsSuite.Features.AllocationBuddy.Views.ConfirmDialog();
        confirm.SetMessage($"Move all items from '{loc.Location}' to the Item Pool? This will clear the store.");
        var ok = await _dialogService.ShowContentDialogAsync<bool?>(confirm);
        if (ok != true) return;

        // remember the deactivation for undo
        var snapshot = loc.Items.Select(i => new ItemSnapshot { ItemNumber = i.ItemNumber, Description = i.Description, Quantity = i.Quantity, SKU = i.SKU }).ToList();
        _lastDeactivation = new DeactivationRecord { Location = loc, Items = snapshot };
        // notify Undo command availability
        (UndoDeactivateCommand as RelayCommand)?.NotifyCanExecuteChanged();

        // move all items from this location into the pool
        var items = loc.Items.ToList();
        foreach (var it in items)
        {
            if (it == null) continue;
            var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == it.ItemNumber && p.SKU == it.SKU);
            if (poolItem == null)
            {
                ItemPool.Add(new ItemAllocation
                {
                    ItemNumber = it.ItemNumber,
                    Description = it.Description,
                    Quantity = it.Quantity,
                    SKU = it.SKU
                });
            }
            else
            {
                poolItem.Quantity += it.Quantity;
                poolItem.IsUpdated = true;
                _ = Task.Delay(300).ContinueWith(_ => poolItem.IsUpdated = false);
            }
        }

        // clear the location's items and soft-disable
        loc.Items.Clear();
        loc.IsActive = false;

        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
    }

    private async Task ImportAsync()
    {
        var files = await _dialogService.ShowOpenFileDialogAsync("Select allocation file", "All Files", "xlsx", "csv");
        if (files == null || files.Length == 0) return;
        var file = files[0];
        Result<IReadOnlyList<AllocationEntry>> result;
        if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            result = await _parser.ParseCsvAsync(file);
        }
        else
        {
            result = await _parser.ParseExcelAsync(file);
        }

        if (!result.IsSuccess || result.Value == null)
        {
            StatusMessage = $"Import failed: {result.ErrorMessage}";
            return;
        }

        PopulateFromEntries(result.Value);
        StatusMessage = $"Imported {result.Value.Count} entries";
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(TotalEntries));
    }

    /// <summary>
    /// Import multiple files (used by drag & drop). Accepts full file paths.
    /// </summary>
    public async Task ImportFilesAsync(string[] files)
    {
        if (files == null || files.Length == 0) return;

        FileImportResults.Clear();
        var allEntries = new List<AllocationEntry>();
        foreach (var file in files)
        {
            try
            {
                Result<IReadOnlyList<AllocationEntry>> r;
                if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    r = await _parser.ParseCsvAsync(file);
                else
                    r = await _parser.ParseExcelAsync(file);

                if (!r.IsSuccess)
                {
                    FileImportResults.Add(new FileImportResult { FileName = Path.GetFileName(file), Success = false, Message = r.ErrorMessage ?? "Parse failed", Count = 0 });
                    continue;
                }

                if (r.Value != null)
                {
                    allEntries.AddRange(r.Value);
                    FileImportResults.Add(new FileImportResult { FileName = Path.GetFileName(file), Success = true, Message = "Imported", Count = r.Value.Count });
                }
                else
                {
                    FileImportResults.Add(new FileImportResult { FileName = Path.GetFileName(file), Success = false, Message = "No entries parsed", Count = 0 });
                }
            }
            catch (Exception ex)
            {
                FileImportResults.Add(new FileImportResult { FileName = Path.GetFileName(file), Success = false, Message = ex.Message, Count = 0 });
            }
        }

        if (allEntries.Count > 0)
        {
            PopulateFromEntries(allEntries);
            StatusMessage = $"Imported {allEntries.Count} entries from {files.Length} files";
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(TotalEntries));
        }
    }

    private Task RefreshAsync()
    {
        // simple refresh - recompute totals
        OnPropertyChanged(nameof(TotalEntries));
        return Task.CompletedTask;
    }

    private void PopulateFromEntries(IReadOnlyList<AllocationEntry> entries)
    {
        LocationAllocations.Clear();
        ItemPool.Clear();

        var grouped = entries.GroupBy(e => e.StoreId ?? "Unknown");
        foreach (var g in grouped.OrderBy(x => x.Key))
        {
            var loc = new LocationAllocation { Location = g.Key };
            foreach (var e in g)
            {
                loc.Items.Add(new ItemAllocation { ItemNumber = e.ItemNumber, Description = e.Description ?? "", Quantity = e.Quantity, SKU = GetSKU(e) });
            }
            LocationAllocations.Add(loc);
        }
    }

    // Command to open selection dialog and move from pool to chosen location
    public IRelayCommand<ItemAllocation> SelectAndMoveFromPoolCommand => new RelayCommand<ItemAllocation>(async (item) => await SelectAndMoveAsync(item));

    private async Task SelectAndMoveAsync(ItemAllocation item)
    {
        if (item == null) return;
        var locations = LocationAllocations.Select(l => l.Location).ToList();
        // show simple dialog using DialogService
        var dialogVm = new SelectLocationDialogViewModel();
        foreach (var loc in locations) dialogVm.Locations.Add(loc);

        var dialog = new BusinessToolsSuite.Features.AllocationBuddy.Views.SelectLocationDialog
        {
            DataContext = dialogVm
        };

        var result = await _dialogService.ShowContentDialogAsync<SelectLocationDialogViewModel?>(dialog);
        if (result != null && !string.IsNullOrWhiteSpace(result.SelectedLocation))
        {
            // perform move to selected location using requested quantity
            var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == item.ItemNumber);
            if (poolItem == null || poolItem.Quantity <= 0) return;
            var qtyToMove = Math.Max(1, result.SelectedQuantity);
            qtyToMove = Math.Min(qtyToMove, poolItem.Quantity);

            var loc = LocationAllocations.FirstOrDefault(l => l.Location == result.SelectedLocation);
            if (loc == null)
            {
                loc = new LocationAllocation { Location = result.SelectedLocation };
                LocationAllocations.Add(loc);
            }
            var target = loc.Items.FirstOrDefault(i => i.ItemNumber == item.ItemNumber);
            if (target == null) loc.Items.Add(new ItemAllocation { ItemNumber = item.ItemNumber, Description = item.Description, Quantity = qtyToMove, SKU = item.SKU });
            else target.Quantity += qtyToMove;

            poolItem.Quantity -= qtyToMove;
            if (poolItem.Quantity == 0) ItemPool.Remove(poolItem);
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(LocationsCount));
        }
    }

    private string? GetSKU(AllocationEntry e)
    {
        // try to find in parser dictionary
        return null;
    }

    private void RemoveOne(ItemAllocation item)
    {
        if (item == null) return;
        // find the exact location containing this specific item instance
        var loc = LocationAllocations.FirstOrDefault(l => l.Items.Contains(item));
        if (loc == null) return;
        var target = loc.Items.First(i => ReferenceEquals(i, item));
        if (target.Quantity <= 0) return;
        target.Quantity -= 1;
        // add to pool
        var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == target.ItemNumber);
        if (poolItem == null)
        {
            ItemPool.Add(new ItemAllocation { ItemNumber = target.ItemNumber, Description = target.Description, Quantity = 1, SKU = target.SKU });
        }
        else
        {
            poolItem.Quantity += 1;
            poolItem.IsUpdated = true;
            _ = Task.Delay(300).ContinueWith(_ => poolItem.IsUpdated = false);
        }
        // mark target updated
        target.IsUpdated = true;
        _ = Task.Delay(300).ContinueWith(_ => target.IsUpdated = false);
        if (target.Quantity == 0)
            loc.Items.Remove(target);
        OnPropertyChanged(nameof(TotalEntries));
    }

    private void AddOne(ItemAllocation item)
    {
        if (item == null) return;
        // add one from pool to this location if pool has it
        var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == item.ItemNumber);
        if (poolItem == null || poolItem.Quantity <= 0) return;
        // find the exact location containing this item instance (the Add button was clicked in that card)
        var loc = LocationAllocations.FirstOrDefault(l => l.Items.Contains(item));
        if (loc == null)
        {
            // add to first location
            if (LocationAllocations.Count == 0)
            {
                // create default location
                var newLoc = new LocationAllocation { Location = "Unassigned" };
                newLoc.Items.Add(new ItemAllocation { ItemNumber = item.ItemNumber, Description = item.Description, Quantity = 1, SKU = item.SKU });
                LocationAllocations.Add(newLoc);
            }
            else
            {
                LocationAllocations[0].Items.Add(new ItemAllocation { ItemNumber = item.ItemNumber, Description = item.Description, Quantity = 1, SKU = item.SKU });
            }
        }
        else
        {
            var target = loc.Items.First(i => ReferenceEquals(i, item));
            target.Quantity += 1;
            target.IsUpdated = true;
            _ = Task.Delay(300).ContinueWith(_ => target.IsUpdated = false);
        }

        poolItem.Quantity -= 1;
        poolItem.IsUpdated = true;
        _ = Task.Delay(300).ContinueWith(_ => poolItem.IsUpdated = false);
        if (poolItem.Quantity == 0) ItemPool.Remove(poolItem);
        OnPropertyChanged(nameof(TotalEntries));
    }

    private void MoveFromPool(ItemAllocation item)
    {
        // Move from pool to first location
        if (item == null) return;
        var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == item.ItemNumber);
        if (poolItem == null) return;
        if (LocationAllocations.Count == 0)
        {
            var newLoc = new LocationAllocation { Location = "Unassigned" };
            newLoc.Items.Add(new ItemAllocation { ItemNumber = item.ItemNumber, Description = item.Description, Quantity = 1, SKU = item.SKU });
            LocationAllocations.Add(newLoc);
        }
        else
        {
            var loc = LocationAllocations[0];
            var target = loc.Items.FirstOrDefault(i => i.ItemNumber == item.ItemNumber);
            if (target == null) loc.Items.Add(new ItemAllocation { ItemNumber = item.ItemNumber, Description = item.Description, Quantity = 1, SKU = item.SKU });
            else target.Quantity += 1;
        }

        poolItem.Quantity -= 1;
        poolItem.IsUpdated = true;
        _ = Task.Delay(300).ContinueWith(_ => poolItem.IsUpdated = false);
        if (poolItem.Quantity == 0) ItemPool.Remove(poolItem);
        OnPropertyChanged(nameof(TotalEntries));
    }

    private void ClearSearch()
    {
        // Clear search text and refresh view
        SearchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        _ = RefreshAsync();
    }

    private void UndoDeactivate()
    {
        if (_lastDeactivation == null || _lastDeactivation.Location == null) return;

        var loc = _lastDeactivation.Location;
        // restore items from pool back to location where possible
        foreach (var snap in _lastDeactivation.Items)
        {
            var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == snap.ItemNumber && p.SKU == snap.SKU);
            var qtyAvailable = poolItem?.Quantity ?? 0;
            var qtyToRestore = Math.Min(snap.Quantity, qtyAvailable);
            if (qtyToRestore <= 0) continue;

            var existing = loc.Items.FirstOrDefault(i => i.ItemNumber == snap.ItemNumber && i.SKU == snap.SKU);
            if (existing == null)
            {
                loc.Items.Add(new ItemAllocation { ItemNumber = snap.ItemNumber, Description = snap.Description, Quantity = qtyToRestore, SKU = snap.SKU });
            }
            else
            {
                existing.Quantity += qtyToRestore;
            }

            if (poolItem != null)
            {
                poolItem.Quantity -= qtyToRestore;
                if (poolItem.Quantity <= 0) ItemPool.Remove(poolItem);
            }
        }

        loc.IsActive = true;
        _lastDeactivation = null;
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
    }

    public class LocationAllocation
    {
        public string Location { get; set; } = string.Empty;
        public ObservableCollection<ItemAllocation> Items { get; } = new();
        public bool IsActive { get; set; } = true;
    }

    private class ItemSnapshot
    {
        public string ItemNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? SKU { get; set; }
    }

    private class DeactivationRecord
    {
        public LocationAllocation? Location { get; set; }
        public List<ItemSnapshot> Items { get; set; } = new();
    }

    public class FileImportResult
    {
        public string FileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public partial class ItemAllocation : ObservableObject
    {
        [ObservableProperty]
        private string _itemNumber = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private int _quantity;

        [ObservableProperty]
        private string? _sKU;

        [ObservableProperty]
        private bool _isUpdated;
    }
}
