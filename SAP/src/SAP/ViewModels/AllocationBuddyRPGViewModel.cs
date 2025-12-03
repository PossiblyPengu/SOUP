using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SAP.Core.Entities.AllocationBuddy;
using SAP.Infrastructure.Services.Parsers;
using SAP.Helpers;
using SAP.Services;
using SAP.Data;
using SAP.Core.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;

namespace SAP.ViewModels;

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

    [ObservableProperty]
    private string _pasteText = string.Empty;

    /// <summary>
    /// True when there is no data loaded (for welcome screen)
    /// </summary>
    public bool HasNoData => LocationAllocations.Count == 0 && ItemPool.Count == 0;
    
    /// <summary>
    /// True when there is data loaded
    /// </summary>
    public bool HasData => !HasNoData;

    /// <summary>
    /// Called when SearchText changes to filter the displayed locations.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredLocationAllocations));
    }

    /// <summary>
    /// Gets locations filtered by search text.
    /// </summary>
    public IEnumerable<LocationAllocation> FilteredLocationAllocations
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return LocationAllocations;

            var search = SearchText.Trim().ToLowerInvariant();
            return LocationAllocations.Where(loc =>
                loc.Location.ToLowerInvariant().Contains(search) ||
                (loc.LocationName?.ToLowerInvariant().Contains(search) ?? false) ||
                loc.Items.Any(i =>
                    i.ItemNumber.ToLowerInvariant().Contains(search) ||
                    (i.Description?.ToLowerInvariant().Contains(search) ?? false)));
        }
    }

    public int TotalEntries => LocationAllocations.Sum(l => l.Items.Sum(i => i.Quantity));

    public int LocationsCount => LocationAllocations.Count;

    public int ItemPoolCount => ItemPool.Count;

    public string FooterMessage => StatusMessage;

    /// <summary>
    /// Gets a summary of total quantities per unique item across all locations.
    /// </summary>
    public ObservableCollection<ItemTotalSummary> ItemTotals { get; } = new();

    /// <summary>
    /// Recalculates the ItemTotals collection from all locations.
    /// </summary>
    private void RefreshItemTotals()
    {
        var totals = LocationAllocations
            .SelectMany(l => l.Items)
            .GroupBy(i => i.ItemNumber)
            .Select(g => new ItemTotalSummary
            {
                ItemNumber = g.Key,
                Description = g.First().Description,
                TotalQuantity = g.Sum(i => i.Quantity),
                LocationCount = g.Select(i => i).Count()
            })
            .OrderByDescending(t => t.TotalQuantity)
            .ToList();

        ItemTotals.Clear();
        foreach (var t in totals)
        {
            ItemTotals.Add(t);
        }
        OnPropertyChanged(nameof(ItemTotals));
    }

    public IRelayCommand ImportCommand { get; }
    public IAsyncRelayCommand<string[]> ImportFilesCommand { get; }
    public IRelayCommand PasteCommand { get; }
    public IRelayCommand ImportFromPasteTextCommand { get; }
    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand RemoveOneCommand { get; }
    public IRelayCommand AddOneCommand { get; }
    public IRelayCommand MoveFromPoolCommand { get; }
    public IAsyncRelayCommand<LocationAllocation> DeactivateStoreCommand { get; }
    public IRelayCommand UndoDeactivateCommand { get; }
    public IAsyncRelayCommand ClearDataCommand { get; }
    public IRelayCommand<LocationAllocation> CopyLocationToClipboardCommand { get; }
    public IAsyncRelayCommand ExportToExcelCommand { get; }
    public IAsyncRelayCommand ExportToCsvCommand { get; }

    public AllocationBuddyRPGViewModel(AllocationBuddyParser parser, DialogService dialogService, ILogger<AllocationBuddyRPGViewModel>? logger = null)
    {
        _parser = parser;
        _dialogService = dialogService;
        _logger = logger;

        ImportCommand = new RelayCommand(async () => await ImportAsync());
        ImportFilesCommand = new AsyncRelayCommand<string[]?>(async files => { if (files != null) await ImportFilesAsync(files); });
        PasteCommand = new RelayCommand(PasteFromClipboard);
        ImportFromPasteTextCommand = new RelayCommand(ImportFromPasteText);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        ClearCommand = new RelayCommand(ClearSearch);
        RemoveOneCommand = new RelayCommand<ItemAllocation?>(item => { if (item != null) RemoveOne(item); });
        AddOneCommand = new RelayCommand<ItemAllocation?>(item => { if (item != null) AddOne(item); });
        MoveFromPoolCommand = new RelayCommand<ItemAllocation?>(item => { if (item != null) MoveFromPool(item); });
        DeactivateStoreCommand = new AsyncRelayCommand<LocationAllocation?>(async loc => { if (loc != null) await DeactivateStoreAsync(loc); });
        UndoDeactivateCommand = new RelayCommand(UndoDeactivate, () => _lastDeactivation != null);
        ClearDataCommand = new AsyncRelayCommand(ClearDataAsync);
        CopyLocationToClipboardCommand = new RelayCommand<LocationAllocation>(CopyLocationToClipboard);
        ExportToExcelCommand = new AsyncRelayCommand(ExportToExcelAsync);
        ExportToCsvCommand = new AsyncRelayCommand(ExportToCsvAsync);

        // Load dictionaries on a background task to avoid blocking the UI during startup
        _ = Task.Run(() =>
        {
            try
            {
                // Load store dictionary from shared location
                var stores = InternalStoreDictionary.GetStores();
                var mappedStores = stores.Select(s => new StoreEntry { Code = s.Code, Name = s.Name, Rank = s.Rank }).ToList();
                _parser.SetStoreDictionary(mappedStores);

                // Load item dictionary from shared location
                var items = InternalItemDictionary.GetItems();
                _parser.SetDictionaryItems(items);
                Serilog.Log.Information("Loaded {Count} dictionary items for AllocationBuddy matching", items.Count);
            }
            catch (Exception ex)
            {
                // Log via Serilog if available; otherwise ignore to avoid throwing from ctor
                try { Serilog.Log.Error(ex, "Failed to load dictionaries for AllocationBuddy"); } catch { }
            }
        });
    }

    /// <summary>
    /// Copies the items from a location to the clipboard in a tab-separated format
    /// suitable for pasting into Business Central transfer orders.
    /// Format: ItemNumber[TAB]Quantity per line
    /// </summary>
    private void CopyLocationToClipboard(LocationAllocation? loc)
    {
        if (loc == null || loc.Items.Count == 0)
        {
            StatusMessage = "No items to copy";
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var item in loc.Items)
        {
            // Tab-separated: Item Number, Quantity
            sb.AppendLine($"{item.ItemNumber}\t{item.Quantity}");
        }

        try
        {
            System.Windows.Clipboard.SetText(sb.ToString());
            StatusMessage = $"Copied {loc.Items.Count} items for '{loc.Location}' to clipboard";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    private async Task DeactivateStoreAsync(LocationAllocation loc)
    {
        if (loc == null) return;

        // Confirm first (await the dialog instead of blocking)
        var confirm = new SAP.Views.AllocationBuddy.ConfirmDialog();
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

            var itemNumber = GetCanonicalItemNumber(it.ItemNumber);
            var description = string.IsNullOrWhiteSpace(it.Description) ? GetDescription(itemNumber) : it.Description;
            var sku = it.SKU ?? GetSKU(itemNumber);

            var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == itemNumber && p.SKU == sku);
            if (poolItem == null)
            {
                ItemPool.Add(new ItemAllocation
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
        OnPropertyChanged(nameof(FilteredLocationAllocations));
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
        OnPropertyChanged(nameof(FilteredLocationAllocations));
    }

    /// <summary>
    /// Paste allocation data from clipboard (copied from Excel or other spreadsheet).
    /// Expects tab or comma separated data with Store, Item, Quantity columns.
    /// </summary>
    private void PasteFromClipboard()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
            {
                StatusMessage = "Clipboard is empty";
                return;
            }

            var clipboardText = System.Windows.Clipboard.GetText();
            var result = _parser.ParseFromClipboardText(clipboardText);

            if (!result.IsSuccess || result.Value == null)
            {
                StatusMessage = $"Paste failed: {result.ErrorMessage}";
                return;
            }

            PopulateFromEntries(result.Value);
            StatusMessage = $"Pasted {result.Value.Count} entries from clipboard";
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Paste failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to paste from clipboard");
        }
    }

    /// <summary>
    /// Import data from the PasteText textbox on the welcome screen.
    /// </summary>
    private void ImportFromPasteText()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PasteText))
            {
                StatusMessage = "Please paste some data first";
                return;
            }

            var result = _parser.ParseFromClipboardText(PasteText);

            if (!result.IsSuccess || result.Value == null)
            {
                StatusMessage = $"Import failed: {result.ErrorMessage}";
                return;
            }

            PopulateFromEntries(result.Value);
            PasteText = string.Empty; // Clear the textbox after successful import
            StatusMessage = $"Imported {result.Value.Count} entries";
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to import from paste text");
        }
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
            OnPropertyChanged(nameof(FilteredLocationAllocations));
        }
    }

    private Task RefreshAsync()
    {
        // simple refresh - recompute totals
        OnPropertyChanged(nameof(TotalEntries));
        RefreshItemTotals();
        return Task.CompletedTask;
    }

    private void PopulateFromEntries(IReadOnlyList<AllocationEntry> entries)
    {
        LocationAllocations.Clear();
        ItemPool.Clear();

        var grouped = entries.GroupBy(e => e.StoreId ?? "Unknown");
        foreach (var g in grouped.OrderBy(x => x.Key))
        {
            // Store both code and name for display
            var storeCode = g.Key;
            var storeName = g.FirstOrDefault()?.StoreName;
            
            // Try to resolve both code and name from dictionary
            var storeFromDict = InternalStoreDictionary.FindByCode(storeCode);
            
            if (storeFromDict != null)
            {
                // Found by code - use the name from dictionary
                if (string.IsNullOrWhiteSpace(storeName) || storeName.Equals(storeCode, StringComparison.OrdinalIgnoreCase))
                {
                    storeName = storeFromDict.Name;
                }
            }
            else
            {
                // Not found by code - maybe the "code" is actually a name, try searching by name
                var storesByName = InternalStoreDictionary.SearchByName(storeCode, 1);
                if (storesByName.Count > 0)
                {
                    var matchedStore = storesByName[0];
                    // Check if it's an exact match (case-insensitive)
                    if (matchedStore.Name.Equals(storeCode, StringComparison.OrdinalIgnoreCase))
                    {
                        storeCode = matchedStore.Code;
                        storeName = matchedStore.Name;
                    }
                }
            }
            
            var loc = new LocationAllocation { Location = storeCode, LocationName = storeName };
            foreach (var e in g)
            {
                var itemNumber = GetCanonicalItemNumber(e.ItemNumber ?? "");
                var description = string.IsNullOrWhiteSpace(e.Description) ? GetDescription(itemNumber) : e.Description;
                var sku = e.SKU ?? GetSKU(itemNumber);

                loc.Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = e.Quantity,
                    SKU = sku
                });
            }
            LocationAllocations.Add(loc);
        }

        RefreshItemTotals();
        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(HasData));
    }

    // Command to open selection dialog and move from pool to chosen location
    public IRelayCommand<ItemAllocation?> SelectAndMoveFromPoolCommand => new RelayCommand<ItemAllocation?>(async (item) => { if (item != null) await SelectAndMoveAsync(item); });

    private async Task SelectAndMoveAsync(ItemAllocation item)
    {
        if (item == null) return;
        var locations = LocationAllocations.Select(l => l.Location).ToList();
        // show simple dialog using DialogService
        var dialogVm = new SelectLocationDialogViewModel();
        foreach (var loc in locations) dialogVm.Locations.Add(loc);

        var dialog = new SAP.Views.AllocationBuddy.SelectLocationDialog
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

            var itemNumber = GetCanonicalItemNumber(item.ItemNumber);
            var description = string.IsNullOrWhiteSpace(item.Description) ? GetDescription(itemNumber) : item.Description;
            var sku = item.SKU ?? GetSKU(itemNumber);

            var loc = LocationAllocations.FirstOrDefault(l => l.Location == result.SelectedLocation);
            if (loc == null)
            {
                loc = new LocationAllocation { Location = result.SelectedLocation };
                LocationAllocations.Add(loc);
            }
            var target = loc.Items.FirstOrDefault(i => i.ItemNumber == item.ItemNumber);
            if (target == null)
            {
                loc.Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = qtyToMove,
                    SKU = sku
                });
            }
            else
            {
                target.Quantity += qtyToMove;
            }

            poolItem.Quantity -= qtyToMove;
            if (poolItem.Quantity == 0) ItemPool.Remove(poolItem);
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(LocationsCount));
        }
    }

    private string? GetSKU(string itemNumber)
    {
        var dictItem = _parser.DictionaryItems?.FirstOrDefault(d =>
            string.Equals(d.Number, itemNumber, StringComparison.OrdinalIgnoreCase) ||
            (d.Skus != null && d.Skus.Contains(itemNumber, StringComparer.OrdinalIgnoreCase)));
        return dictItem?.Skus?.FirstOrDefault();
    }

    private string GetDescription(string itemNumber)
    {
        var dictItem = _parser.DictionaryItems?.FirstOrDefault(d =>
            string.Equals(d.Number, itemNumber, StringComparison.OrdinalIgnoreCase) ||
            (d.Skus != null && d.Skus.Contains(itemNumber, StringComparer.OrdinalIgnoreCase)));
        return dictItem?.Description ?? "";
    }

    private string GetCanonicalItemNumber(string itemNumber)
    {
        var dictItem = _parser.DictionaryItems?.FirstOrDefault(d =>
            string.Equals(d.Number, itemNumber, StringComparison.OrdinalIgnoreCase) ||
            (d.Skus != null && d.Skus.Contains(itemNumber, StringComparer.OrdinalIgnoreCase)));
        return dictItem?.Number ?? itemNumber;
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
            var itemNumber = GetCanonicalItemNumber(target.ItemNumber);
            var description = string.IsNullOrWhiteSpace(target.Description) ? GetDescription(itemNumber) : target.Description;
            var sku = target.SKU ?? GetSKU(itemNumber);

            ItemPool.Add(new ItemAllocation
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
            var itemNumber = GetCanonicalItemNumber(item.ItemNumber);
            var description = string.IsNullOrWhiteSpace(item.Description) ? GetDescription(itemNumber) : item.Description;
            var sku = item.SKU ?? GetSKU(itemNumber);

            if (LocationAllocations.Count == 0)
            {
                // create default location
                var newLoc = new LocationAllocation { Location = "Unassigned" };
                newLoc.Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = 1,
                    SKU = sku
                });
                LocationAllocations.Add(newLoc);
            }
            else
            {
                LocationAllocations[0].Items.Add(new ItemAllocation
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

        var itemNumber = GetCanonicalItemNumber(item.ItemNumber);
        var description = string.IsNullOrWhiteSpace(item.Description) ? GetDescription(itemNumber) : item.Description;
        var sku = item.SKU ?? GetSKU(itemNumber);

        if (LocationAllocations.Count == 0)
        {
            var newLoc = new LocationAllocation { Location = "Unassigned" };
            newLoc.Items.Add(new ItemAllocation
            {
                ItemNumber = itemNumber,
                Description = description,
                Quantity = 1,
                SKU = sku
            });
            LocationAllocations.Add(newLoc);
        }
        else
        {
            var loc = LocationAllocations[0];
            var target = loc.Items.FirstOrDefault(i => i.ItemNumber == item.ItemNumber);
            if (target == null)
            {
                loc.Items.Add(new ItemAllocation
                {
                    ItemNumber = itemNumber,
                    Description = description,
                    Quantity = 1,
                    SKU = sku
                });
            }
            else
            {
                target.Quantity += 1;
            }
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

            var itemNumber = GetCanonicalItemNumber(snap.ItemNumber);
            var description = string.IsNullOrWhiteSpace(snap.Description) ? GetDescription(itemNumber) : snap.Description;
            var sku = snap.SKU ?? GetSKU(itemNumber);

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
                if (poolItem.Quantity <= 0) ItemPool.Remove(poolItem);
            }
        }

        loc.IsActive = true;
        _lastDeactivation = null;
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(FilteredLocationAllocations));
    }

    private async Task ClearDataAsync()
    {
        // Confirm before clearing all data
        var confirm = new SAP.Views.AllocationBuddy.ConfirmDialog();
        confirm.SetMessage("Clear all locations and items? This action cannot be undone.");
        var ok = await _dialogService.ShowContentDialogAsync<bool?>(confirm);
        if (ok != true) return;

        // Clear all collections
        LocationAllocations.Clear();
        ItemPool.Clear();
        FileImportResults.Clear();
        _lastDeactivation = null;
        SearchText = string.Empty;

        // Update all computed properties
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(FilteredLocationAllocations));
        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(HasData));

        StatusMessage = "All data cleared";
        _logger?.LogInformation("All allocation data cleared by user");
    }

    private Task ExportToExcelAsync()
    {
        try
        {
            if (LocationAllocations.Count == 0)
            {
                StatusMessage = "No data to export";
                return Task.CompletedTask;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"AllocationBuddy_Export_{timestamp}.xlsx";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = Path.Combine(desktopPath, fileName);

            // Create Excel file using ClosedXML
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Allocations");

            // Headers
            worksheet.Cell(1, 1).Value = "Location";
            worksheet.Cell(1, 2).Value = "Location Name";
            worksheet.Cell(1, 3).Value = "Item Number";
            worksheet.Cell(1, 4).Value = "Description";
            worksheet.Cell(1, 5).Value = "Quantity";
            worksheet.Cell(1, 6).Value = "SKU";

            // Style headers
            var headerRange = worksheet.Range(1, 1, 1, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

            // Data
            int row = 2;
            foreach (var location in LocationAllocations)
            {
                foreach (var item in location.Items)
                {
                    worksheet.Cell(row, 1).Value = location.Location;
                    worksheet.Cell(row, 2).Value = location.LocationName ?? "";
                    worksheet.Cell(row, 3).Value = item.ItemNumber;
                    worksheet.Cell(row, 4).Value = item.Description;
                    worksheet.Cell(row, 5).Value = item.Quantity;
                    worksheet.Cell(row, 6).Value = item.SKU ?? "";
                    row++;
                }
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);

            StatusMessage = $"Exported to {fileName}";
            _logger?.LogInformation("Exported allocations to Excel: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to export allocations to Excel");
        }
        return Task.CompletedTask;
    }

    private async Task ExportToCsvAsync()
    {
        try
        {
            if (LocationAllocations.Count == 0)
            {
                StatusMessage = "No data to export";
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"AllocationBuddy_Export_{timestamp}.csv";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = Path.Combine(desktopPath, fileName);

            using var writer = new StreamWriter(filePath);
            
            // Headers
            await writer.WriteLineAsync("Location,Location Name,Item Number,Description,Quantity,SKU");

            // Data
            foreach (var location in LocationAllocations)
            {
                foreach (var item in location.Items)
                {
                    var locationName = EscapeCsvField(location.LocationName ?? "");
                    var description = EscapeCsvField(item.Description);
                    var sku = EscapeCsvField(item.SKU ?? "");
                    await writer.WriteLineAsync($"{location.Location},{locationName},{item.ItemNumber},{description},{item.Quantity},{sku}");
                }
            }

            StatusMessage = $"Exported to {fileName}";
            _logger?.LogInformation("Exported allocations to CSV: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to export allocations to CSV");
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

    public class LocationAllocation
    {
        public string Location { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public string DisplayLocation
        {
            get
            {
                // If no name or name equals code, just show code
                if (string.IsNullOrWhiteSpace(LocationName) || 
                    LocationName.Equals(Location, StringComparison.OrdinalIgnoreCase))
                {
                    return Location;
                }
                // Show both: "101 - Downtown Store"
                return $"{Location} - {LocationName}";
            }
        }
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

    /// <summary>
    /// Summary of total quantity for a unique item across all locations.
    /// </summary>
    public class ItemTotalSummary
    {
        public string ItemNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public int LocationCount { get; set; }
    }
}
