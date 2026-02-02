using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SOUP.Core.Common;
using SOUP.Core.Entities.AllocationBuddy;
using SOUP.Data;
using SOUP.Helpers;
using SOUP.Infrastructure.Services.Parsers;
using SOUP.Services;

namespace SOUP.ViewModels;
public partial class AllocationBuddyRPGViewModel : ObservableObject, IDisposable
{

    private static readonly System.Text.Json.JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private readonly AllocationBuddyParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AllocationBuddyRPGViewModel>? _logger;
    private readonly Infrastructure.Services.SettingsService? _settingsService;

    // AllocationBuddy services
    private readonly Services.AllocationBuddy.ItemDictionaryService _dictionaryService;
    private readonly Services.AllocationBuddy.AllocationCalculationService _calculationService;
    private readonly Services.AllocationBuddy.AllocationClipboardService _clipboardService;
    private readonly Services.AllocationBuddy.AllocationExportService _exportService;
    private readonly Services.AllocationBuddy.AllocationImportService _importService;
    private readonly Services.AllocationBuddy.AllocationPersistenceService _persistenceService;
    private readonly Services.AllocationBuddy.AllocationBuddyConfiguration _configuration;

    // Runtime settings loaded from SettingsService
    private bool _showConfirmationDialogs = true;
    private bool _includeDescriptionsInCopy = false;
    private string _clipboardFormat = "TabSeparated";
    private bool _disposed;
    private bool _hasUnarchivedChanges;
    private string? _lastImportedFileName;
    private DeactivationRecord? _lastDeactivation;
    public ObservableCollection<LocationAllocation> LocationAllocations { get; } = new();
    public ObservableCollection<ItemAllocation> ItemPool { get; } = new();
    public ObservableCollection<FileImportResult> FileImportResults { get; } = new();
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredLocationAllocations));
        OnPropertyChanged(nameof(FilteredItemAllocations));
    }
    [ObservableProperty]
    private string _pasteText = string.Empty;
    public bool HasNoData => LocationAllocations.Count == 0 && ItemPool.Count == 0;
    public bool HasData => !HasNoData;
    public int TotalEntries => LocationAllocations.Sum(l => l.Items.Sum(i => i.Quantity));
    public int LocationsCount => LocationAllocations.Count;
    public int ItemPoolCount => ItemPool.Count;
    public IEnumerable<LocationAllocation> FilteredLocationAllocations
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return LocationAllocations;

            var search = SearchText.Trim();
            return LocationAllocations.Where(loc =>
                loc.Location.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (loc.LocationName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                loc.Items.Any(i =>
                    i.ItemNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (i.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)));
        }
    }
    public ObservableCollection<ItemTotalSummary> ItemTotals { get; } = new();
    private string _itemTotalsSortMode = "qty-desc";
    public string ItemTotalsSortMode
    {
        get => _itemTotalsSortMode;
        set
        {
            if (SetProperty(ref _itemTotalsSortMode, value))
            {
                RefreshItemTotals();
            }
        }
    }
    private void RefreshItemTotals()
    {
        var totals = _calculationService.CalculateItemTotals(LocationAllocations, ItemPool, _itemTotalsSortMode);
        ItemTotals.Clear();
        foreach (var t in totals) ItemTotals.Add(t);
        var locationByItem = LocationAllocations.SelectMany(l => l.Items).GroupBy(i => i.ItemNumber, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity), StringComparer.OrdinalIgnoreCase);
        foreach (var poolItem in ItemPool) poolItem.TotalInLocations = locationByItem.TryGetValue(poolItem.ItemNumber, out var quantity) ? quantity : 0;
        OnPropertyChanged(nameof(ItemTotals));
        if (ViewMode == "items") RefreshItemAllocations();
    }
    [ObservableProperty]
    private string _viewMode = "stores";

    partial void OnViewModeChanged(string value)
    {
        if (value == "items")
        {
            RefreshItemAllocations();
        }
        OnPropertyChanged(nameof(IsStoreView));
        OnPropertyChanged(nameof(IsItemView));
    }

    /// <summary>True when viewing by store (default view).</summary>
    public bool IsStoreView => ViewMode == "stores";

    /// <summary>True when viewing by item.</summary>
    public bool IsItemView => ViewMode == "items";
    public ObservableCollection<ItemAllocationView> ItemAllocations { get; } = new();
    public IEnumerable<ItemAllocationView> FilteredItemAllocations
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return ItemAllocations;

            var search = SearchText.Trim();
            return ItemAllocations.Where(item =>
                item.ItemNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (item.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.StoreAllocations.Any(s =>
                    s.StoreCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (s.StoreName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)));
        }
    }
    private void RefreshItemAllocations()
    {
        var allItems = LocationAllocations.SelectMany(loc => loc.Items.Select(item => new { Location = loc, Item = item })).GroupBy(x => x.Item.ItemNumber, StringComparer.OrdinalIgnoreCase).Select(g => new ItemAllocationView { ItemNumber = g.Key, Description = g.First().Item.Description ?? "", TotalQuantity = g.Sum(x => x.Item.Quantity), StoreAllocations = new ObservableCollection<StoreAllocationEntry>(g.Select(x => new StoreAllocationEntry { StoreCode = x.Location.Location, StoreName = x.Location.LocationName, Quantity = x.Item.Quantity, Item = x.Item }).OrderBy(s => s.StoreCode)) }).OrderBy(i => i.ItemNumber).ToList();
        ItemAllocations.Clear();
        foreach (var item in allItems) ItemAllocations.Add(item);
        OnPropertyChanged(nameof(FilteredItemAllocations));
    }
    /// <summary>Command to import data from an Excel or CSV file via file dialog.</summary>
    public IAsyncRelayCommand ImportCommand { get; }

    /// <summary>Command to import data from multiple files (used by drag-and-drop).</summary>
    public IAsyncRelayCommand<string[]> ImportFilesCommand { get; }

    /// <summary>Command to paste and import data from the clipboard.</summary>
    public IRelayCommand PasteCommand { get; }

    /// <summary>Command to import data from the paste text box on the welcome screen.</summary>
    public IRelayCommand ImportFromPasteTextCommand { get; }

    /// <summary>Command to refresh the current data display.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Command to clear the search filter.</summary>
    public IRelayCommand ClearCommand { get; }

    /// <summary>Command to decrease an item's quantity by one.</summary>
    public IRelayCommand RemoveOneCommand { get; }

    /// <summary>Command to increase an item's quantity by one.</summary>
    public IRelayCommand AddOneCommand { get; }

    /// <summary>Command to move an item from the pool to a selected location.</summary>
    public IRelayCommand MoveFromPoolCommand { get; }

    /// <summary>Command to deactivate a store, moving its items to the pool.</summary>
    public IAsyncRelayCommand<LocationAllocation> DeactivateStoreCommand { get; }

    /// <summary>Command to undo the last store deactivation.</summary>
    public IRelayCommand UndoDeactivateCommand { get; }

    /// <summary>Command to clear all allocation data after confirmation.</summary>
    public IAsyncRelayCommand ClearDataCommand { get; }

    /// <summary>Command to copy a location's data to the clipboard.</summary>
    public IRelayCommand<LocationAllocation> CopyLocationToClipboardCommand { get; }

    /// <summary>Command to copy an item's redistribution data (all location allocations) to the clipboard.</summary>
    public IRelayCommand<ItemAllocationView> CopyItemRedistributionCommand { get; }

    /// <summary>Command to export allocation data to an Excel file.</summary>
    public IAsyncRelayCommand ExportToExcelCommand { get; }

    /// <summary>Command to export allocation data to a CSV file.</summary>
    public IAsyncRelayCommand ExportToCsvCommand { get; }

    /// <summary>Command to sort item totals by the specified mode.</summary>
    public IRelayCommand<string> SortItemTotalsCommand { get; private set; } = null!;

    /// <summary>Command to set the view mode (stores or items).</summary>
    public IRelayCommand<string> SetViewModeCommand { get; private set; } = null!;

    /// <summary>Command to open the settings window to the Allocation tab.</summary>
    public IRelayCommand OpenSettingsCommand { get; }
    /// <summary>Command to manually archive the current data.</summary>
    public IAsyncRelayCommand ArchiveCurrentCommand { get; }

    /// <summary>Command to load and display the list of archives.</summary>
    public IAsyncRelayCommand ViewArchivesCommand { get; }

    /// <summary>Gets the collection of available archives.</summary>
    public ObservableCollection<ArchiveViewModel> Archives { get; } = new();

    /// <summary>Gets or sets whether the archive panel is open.</summary>
    [ObservableProperty]
    private bool _isArchivePanelOpen;

    partial void OnIsArchivePanelOpenChanged(bool value)
    {
        _logger?.LogInformation("Archive panel open changed to: {Value}", value);
    }
    /// <param name="parser">The parser for processing allocation data.</param>
    /// <param name="dialogService">The service for displaying dialogs.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public AllocationBuddyRPGViewModel(AllocationBuddyParser parser, DialogService dialogService, IServiceProvider serviceProvider, Services.AllocationBuddy.ItemDictionaryService dictionaryService, Services.AllocationBuddy.AllocationCalculationService calculationService, Services.AllocationBuddy.AllocationClipboardService clipboardService, Services.AllocationBuddy.AllocationExportService exportService, Services.AllocationBuddy.AllocationImportService importService, Services.AllocationBuddy.AllocationPersistenceService persistenceService, Services.AllocationBuddy.AllocationBuddyConfiguration configuration, ILogger<AllocationBuddyRPGViewModel>? logger = null)
    {
        (_parser, _dialogService, _serviceProvider, _logger, _dictionaryService, _calculationService, _clipboardService, _exportService, _importService, _persistenceService, _configuration) = (parser, dialogService, serviceProvider, logger, dictionaryService ?? throw new ArgumentNullException(nameof(dictionaryService)), calculationService ?? throw new ArgumentNullException(nameof(calculationService)), clipboardService ?? throw new ArgumentNullException(nameof(clipboardService)), exportService ?? throw new ArgumentNullException(nameof(exportService)), importService ?? throw new ArgumentNullException(nameof(importService)), persistenceService ?? throw new ArgumentNullException(nameof(persistenceService)), configuration ?? throw new ArgumentNullException(nameof(configuration)));
        _settingsService = serviceProvider.GetService<Infrastructure.Services.SettingsService>();
        if (_settingsService != null) _settingsService.SettingsChanged += OnSettingsChanged;
        (ImportCommand, OpenSettingsCommand, ImportFilesCommand, PasteCommand, ImportFromPasteTextCommand, RefreshCommand, ClearCommand, RemoveOneCommand, AddOneCommand, MoveFromPoolCommand, DeactivateStoreCommand, UndoDeactivateCommand, ClearDataCommand, CopyLocationToClipboardCommand, CopyItemRedistributionCommand, SortItemTotalsCommand, SetViewModeCommand, ExportToExcelCommand, ExportToCsvCommand, ArchiveCurrentCommand, ViewArchivesCommand) = (new AsyncRelayCommand(ImportAsync), new RelayCommand(OpenSettings), new AsyncRelayCommand<string[]?>(async files => { if (files != null) await ImportFilesAsync(files); }), new RelayCommand(PasteFromClipboard), new RelayCommand(ImportFromPasteText), new AsyncRelayCommand(RefreshAsync), new RelayCommand(ClearSearch), new RelayCommand<ItemAllocation?>(item => { if (item != null) RemoveOne(item); }), new RelayCommand<ItemAllocation?>(item => { if (item != null) AddOne(item); }), new RelayCommand<ItemAllocation?>(item => { if (item != null) MoveFromPool(item); }), new AsyncRelayCommand<LocationAllocation?>(async loc => { if (loc != null) await DeactivateStoreAsync(loc); }), new RelayCommand(UndoDeactivate, () => _lastDeactivation != null), new AsyncRelayCommand(ClearDataAsync), new RelayCommand<LocationAllocation>(CopyLocationToClipboard), new RelayCommand<ItemAllocationView>(CopyItemRedistribution), new RelayCommand<string>(mode => { if (mode != null) ItemTotalsSortMode = mode; }), new RelayCommand<string>(mode => { if (mode != null) ViewMode = mode; }), new AsyncRelayCommand(ExportToExcelAsync), new AsyncRelayCommand(ExportToCsvAsync), new AsyncRelayCommand(ArchiveCurrentAsync, () => HasData), new AsyncRelayCommand(LoadArchivesAsync));
        _ = LoadSettingsAsync(); _ = _dictionaryService.LoadDictionariesAsync(); _ = LoadArchivesAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settingsService = _serviceProvider.GetService<Infrastructure.Services.SettingsService>();
            if (settingsService != null)
            {
                var settings = await settingsService.LoadSettingsAsync<Core.Entities.Settings.AllocationBuddySettings>("AllocationBuddy");
                (_showConfirmationDialogs, _includeDescriptionsInCopy, _clipboardFormat) = (settings.ShowConfirmationDialogs, settings.IncludeDescriptionsInCopy, settings.ClipboardFormat);
                _logger?.LogInformation("Applied AllocationBuddy settings: ShowConfirm={Confirm}, IncludeDesc={Desc}, Format={Format}", _showConfirmationDialogs, _includeDescriptionsInCopy, _clipboardFormat);
            }
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Failed to load AllocationBuddy settings, using defaults"); }
    }
    private void OnSettingsChanged(object? sender, Infrastructure.Services.SettingsChangedEventArgs e) { if (e.AppName == "AllocationBuddy") _ = LoadSettingsAsync(); }


    private void CopyLocationToClipboard(LocationAllocation? loc) => StatusMessage = _clipboardService.CopyLocationToClipboard(loc, _includeDescriptionsInCopy, _clipboardFormat);
    private void CopyItemRedistribution(ItemAllocationView? item)
    {
        if (item == null || item.StoreAllocations.Count == 0) { StatusMessage = "No allocations to copy"; return; }
        var separator = _clipboardFormat == "CommaSeparated" ? "," : "\t";
        var sb = new System.Text.StringBuilder();
        foreach (var allocation in item.StoreAllocations) sb.AppendLine($"{allocation.Quantity}{separator}{allocation.StoreCode}");
        try { System.Windows.Clipboard.SetText(sb.ToString()); StatusMessage = $"Copied {item.ItemNumber} redistribution to {item.StoreAllocations.Count} locations"; }
        catch (Exception ex) { StatusMessage = $"Failed to copy: {ex.Message}"; }
    }

    private async Task DeactivateStoreAsync(LocationAllocation loc)
    {
        if (loc == null) return;
        var confirm = new SOUP.Views.AllocationBuddy.ConfirmDialog();
        confirm.SetMessage($"Move all items from '{loc.Location}' to the Item Pool? This will clear the store.");
        if (await _dialogService.ShowContentDialogAsync<bool?>(confirm) != true) return;
        _lastDeactivation = _calculationService.DeactivateStore(loc, ItemPool);
        if (_lastDeactivation == null) return;
        (UndoDeactivateCommand as RelayCommand)?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(TotalEntries)); OnPropertyChanged(nameof(ItemPoolCount)); OnPropertyChanged(nameof(LocationsCount)); OnPropertyChanged(nameof(FilteredLocationAllocations));
        RefreshItemTotals();
    }

    private async Task ImportAsync()
    {
        try
        {
            _logger?.LogInformation("ImportAsync started");
            var files = await _dialogService.ShowOpenFileDialogAsync("Select allocation file", "All Files", "xlsx", "csv");
            _logger?.LogInformation("File dialog returned: {Files}", files != null ? string.Join(", ", files) : "null");
            if (files == null || files.Length == 0) { _logger?.LogInformation("No files selected, returning"); return; }
            await AutoArchiveIfNeededAsync();
            var file = files[0];
            _logger?.LogInformation("Importing file: {File}", file);
            StatusMessage = $"Importing {Path.GetFileName(file)}...";
            var result = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? await _parser.ParseCsvAsync(file) : await _parser.ParseExcelAsync(file);
            _logger?.LogInformation("Parse result: Success={Success}, Count={Count}, Error={Error}", result.IsSuccess, result.Value?.Count ?? 0, result.ErrorMessage);
            if (!result.IsSuccess || result.Value == null) { StatusMessage = $"Import failed: {result.ErrorMessage}"; _logger?.LogError("Import failed: {Error}", result.ErrorMessage); System.Windows.MessageBox.Show($"Import failed: {result.ErrorMessage}", "Import Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error); return; }
            if (result.Value.Count == 0) { StatusMessage = "No valid entries found. Check file has Store, Item, and Quantity columns."; _logger?.LogWarning("No entries found in file"); System.Windows.MessageBox.Show("No valid entries found.\n\nMake sure the file has columns for Store/Location, Item/Product, and Quantity.", "Import Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return; }
            PopulateFromEntries(result.Value); MarkAsModified();
            StatusMessage = $"Imported {result.Value.Count} entries from {Path.GetFileName(file)}";
            _logger?.LogInformation("Successfully imported {Count} entries", result.Value.Count);
            OnPropertyChanged(nameof(LocationsCount)); OnPropertyChanged(nameof(ItemPoolCount)); OnPropertyChanged(nameof(TotalEntries)); OnPropertyChanged(nameof(FilteredLocationAllocations));
            RefreshItemTotals();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Exception in ImportAsync"); StatusMessage = $"Import error: {ex.Message}"; System.Windows.MessageBox.Show($"Import error: {ex.Message}", "Import Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error); }
    }

    private void PasteFromClipboard()
    {
        try
        {
            var dialog = new Views.AllocationBuddy.PasteDataDialog();
            var dialogWindow = new System.Windows.Window { Content = dialog, Title = "Paste Data", SizeToContent = System.Windows.SizeToContent.WidthAndHeight, WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner, Owner = System.Windows.Application.Current.MainWindow, WindowStyle = System.Windows.WindowStyle.None, AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent, ResizeMode = System.Windows.ResizeMode.NoResize, ShowInTaskbar = false };
            if (dialogWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.PastedText)) return;
            var pastedText = dialog.PastedText;
            if (pastedText.Length > _configuration.MaxClipboardTextLengthBytes) { StatusMessage = $"Pasted content too large (max {_configuration.MaxClipboardTextLengthBytes / 1_000_000}MB)"; _logger?.LogWarning("Pasted text rejected: {Length} bytes exceeds maximum", pastedText.Length); return; }
            var parseResult = _parser.ParseFromClipboardText(pastedText);
            if (!parseResult.IsSuccess || parseResult.Value == null) { StatusMessage = $"Import failed: {parseResult.ErrorMessage}"; return; }
            _ = AutoArchiveIfNeededAsync();
            PopulateFromEntries(parseResult.Value); MarkAsModified();
            StatusMessage = $"Imported {parseResult.Value.Count} entries";
            OnPropertyChanged(nameof(LocationsCount)); OnPropertyChanged(nameof(ItemPoolCount)); OnPropertyChanged(nameof(TotalEntries)); OnPropertyChanged(nameof(FilteredLocationAllocations));
            RefreshItemTotals();
        }
        catch (Exception ex) { StatusMessage = $"Import failed: {ex.Message}"; _logger?.LogError(ex, "Failed to import pasted data"); }
    }
    private void ImportFromPasteText()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PasteText))
            {
                StatusMessage = "Please paste some data first";
                return;
            }

            // Validate text length to prevent DoS
            if (PasteText.Length > _configuration.MaxClipboardTextLengthBytes)
            {
                StatusMessage = $"Text too large (max {_configuration.MaxClipboardTextLengthBytes / 1_000_000}MB)";
                _logger?.LogWarning("Paste text rejected: {Length} bytes exceeds maximum", PasteText.Length);
                return;
            }

            var result = _parser.ParseFromClipboardText(PasteText);

            if (!result.IsSuccess || result.Value == null)
            {
                StatusMessage = $"Import failed: {result.ErrorMessage}";
                return;
            }

            // Auto-archive is async, but for paste we'll fire-and-forget
            _ = AutoArchiveIfNeededAsync();

            PopulateFromEntries(result.Value);
            MarkAsModified();
            PasteText = string.Empty; // Clear the textbox after successful import
            StatusMessage = $"Imported {result.Value.Count} entries";
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
            RefreshItemTotals();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to import from paste text");
        }
    }
    public async Task ImportFilesAsync(string[] files)
    {
        if (files == null || files.Length == 0) return;

        FileImportResults.Clear();
        List<AllocationEntry> allEntries = new();
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
            await AutoArchiveIfNeededAsync();
            PopulateFromEntries(allEntries);
            MarkAsModified();

            // Track the imported file name for session saves
            _lastImportedFileName = files.Length == 1
                ? Path.GetFileNameWithoutExtension(files[0])
                : $"{files.Length} files";

            StatusMessage = $"Imported {allEntries.Count} entries from {files.Length} files";
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
            RefreshItemTotals();
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
                var itemNumber = _dictionaryService.GetCanonicalItemNumber(e.ItemNumber ?? "");
                var description = string.IsNullOrWhiteSpace(e.Description) ? _dictionaryService.GetDescription(itemNumber) : e.Description;
                var sku = e.SKU ?? _dictionaryService.GetSKU(itemNumber);

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

        var dialog = new SOUP.Views.AllocationBuddy.SelectLocationDialog
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

            var itemNumber = _dictionaryService.GetCanonicalItemNumber(item.ItemNumber);
            var description = string.IsNullOrWhiteSpace(item.Description) ? _dictionaryService.GetDescription(itemNumber) : item.Description;
            var sku = item.SKU ?? _dictionaryService.GetSKU(itemNumber);

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
            RefreshItemTotals();
        }
    }
    private void SetTemporaryUpdateFlag(ItemAllocation item)
    {
        item.IsUpdated = true;
        _ = Task.Delay(_configuration.UpdateFlashDurationMs).ContinueWith(_ => item.IsUpdated = false);
    }

    private void RemoveOne(ItemAllocation item)
    {
        if (!_calculationService.RemoveOne(item, LocationAllocations, ItemPool))
        {
            return;
        }

        // UI updates
        SetTemporaryUpdateFlag(item);
        var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == item.ItemNumber);
        if (poolItem != null)
        {
            SetTemporaryUpdateFlag(poolItem);
        }

        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        RefreshItemTotals();
        MarkAsModified();
    }

    private void AddOne(ItemAllocation item)
    {
        if (!_calculationService.AddOne(item, LocationAllocations, ItemPool))
        {
            return;
        }

        // UI updates
        SetTemporaryUpdateFlag(item);
        var poolItem = ItemPool.FirstOrDefault(p => p.ItemNumber == item.ItemNumber);
        if (poolItem != null)
        {
            SetTemporaryUpdateFlag(poolItem);
        }

        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(FilteredLocationAllocations));
        RefreshItemTotals();
        MarkAsModified();
    }

    private void MoveFromPool(ItemAllocation item)
    {
        if (!_calculationService.MoveFromPool(item, LocationAllocations, ItemPool))
        {
            return;
        }

        // UI updates
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(FilteredLocationAllocations));
        RefreshItemTotals();
        MarkAsModified();
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
        if (_lastDeactivation == null) return;

        if (!_calculationService.UndoDeactivate(_lastDeactivation, ItemPool))
        {
            return;
        }

        _lastDeactivation = null;
        (UndoDeactivateCommand as RelayCommand)?.NotifyCanExecuteChanged();

        // UI updates
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(FilteredLocationAllocations));
        RefreshItemTotals();
        MarkAsModified();
    }

    private async Task ClearDataAsync()
    {
        // Confirm before clearing all data
        var confirm = new SOUP.Views.AllocationBuddy.ConfirmDialog();
        confirm.SetMessage("Clear all locations and items? This action cannot be undone.");
        var ok = await _dialogService.ShowContentDialogAsync<bool?>(confirm);
        if (ok != true) return;

        // Auto-archive before clearing
        await AutoArchiveIfNeededAsync();

        // Delete session-save archives so cleared state persists across restarts
        DeleteSessionSaveArchives();

        // Clear all collections
        LocationAllocations.Clear();
        ItemPool.Clear();
        FileImportResults.Clear();
        _lastDeactivation = null;
        SearchText = string.Empty;
        _hasUnarchivedChanges = false; // Reset since we just archived and cleared

        // Update all computed properties
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(FilteredLocationAllocations));
        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(HasData));
        RefreshItemTotals();

        StatusMessage = "All data cleared";
        _logger?.LogInformation("All allocation data cleared by user");
    }

    private void OpenSettings()
    {
        try
        {
            var settingsViewModel = _serviceProvider.GetRequiredService<UnifiedSettingsViewModel>();
            var settingsWindow = new Views.UnifiedSettingsWindow(settingsViewModel, "allocation");
            // Only set owner if MainWindow is visible
            if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mainWindow)
            {
                settingsWindow.Owner = mainWindow;
            }

            settingsWindow.Show();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open settings window");
            StatusMessage = "Failed to open settings";
        }
    }
    private void DeleteSessionSaveArchives()
    {
        try
        {
            var archivePath = GetArchivePath();
            if (!Directory.Exists(archivePath)) return;

            // Delete Session saves (Session-Save and Session_*) and Auto-Archive files
            // These are auto-restored on startup, so delete them when user clears data
            var sessionFiles = Directory.GetFiles(archivePath, "*_Session-Save.json")
                .Concat(Directory.GetFiles(archivePath, "*_Session_*.json"))
                .Concat(Directory.GetFiles(archivePath, "*_Auto-Archive.json"));

            foreach (var file in sessionFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger?.LogInformation("Deleted session archive: {FilePath}", file);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete session archive: {FilePath}", file);
                }
            }

            // Clear the imported file name since data is cleared
            _lastImportedFileName = null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete session save archives");
        }
    }

    private async Task ExportToExcelAsync()
    {
        try
        {
            if (LocationAllocations.Count == 0)
            {
                StatusMessage = "No data to export";
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"AllocationBuddy_Export_{timestamp}.xlsx";

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Export to Excel",
                defaultFileName,
                "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*");

            if (string.IsNullOrEmpty(filePath))
            {
                StatusMessage = "Export cancelled";
                return;
            }

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
            int itemCount = 0;
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
                    itemCount++;
                }
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);

            var fileName = Path.GetFileName(filePath);
            StatusMessage = $"Exported {itemCount} item(s)";
            _logger?.LogInformation("Exported allocations to Excel: {FilePath}", filePath);
            _dialogService.ShowExportSuccessDialog(fileName, filePath, itemCount);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to export allocations to Excel");
            _dialogService.ShowExportErrorDialog(ex.Message);
        }
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
            var defaultFileName = $"AllocationBuddy_Export_{timestamp}.csv";

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Export to CSV",
                defaultFileName,
                "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");

            if (string.IsNullOrEmpty(filePath))
            {
                StatusMessage = "Export cancelled";
                return;
            }

            using var writer = new StreamWriter(filePath);

            // Headers
            await writer.WriteLineAsync("Location,Location Name,Item Number,Description,Quantity,SKU");

            // Data
            int itemCount = 0;
            foreach (var location in LocationAllocations)
            {
                foreach (var item in location.Items)
                {
                    var locationName = EscapeCsvField(location.LocationName ?? "");
                    var description = EscapeCsvField(item.Description);
                    var sku = EscapeCsvField(item.SKU ?? "");
                    await writer.WriteLineAsync($"{location.Location},{locationName},{item.ItemNumber},{description},{item.Quantity},{sku}");
                    itemCount++;
                }
            }

            var fileName = Path.GetFileName(filePath);
            StatusMessage = $"Exported {itemCount} item(s)";
            _logger?.LogInformation("Exported allocations to CSV: {FilePath}", filePath);
            _dialogService.ShowExportSuccessDialog(fileName, filePath, itemCount);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to export allocations to CSV");
            _dialogService.ShowExportErrorDialog(ex.Message);
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
    private async Task ArchiveCurrentAsync()
    {
        if (LocationAllocations.Count == 0)
        {
            StatusMessage = "No data to archive";
            return;
        }

        // Show archive dialog
        var dialog = new SOUP.Views.AllocationBuddy.ArchiveDialog();
        var result = await _dialogService.ShowContentDialogAsync<ArchiveDialogResult?>(dialog);

        if (result == null) return;

        try
        {
            var archivePath = GetArchivePath();
            Directory.CreateDirectory(archivePath);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeFileName = string.Join("_", result.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{timestamp}_{safeFileName}.json";
            var filePath = Path.Combine(archivePath, fileName);

            // Create archive data
            var archiveData = new ArchiveData
            {
                Name = result.Name,
                Notes = result.Notes,
                ArchivedAt = DateTime.Now,
                Locations = LocationAllocations.Select(loc => new ArchivedLocation
                {
                    Location = loc.Location,
                    LocationName = loc.LocationName,
                    Items = loc.Items.Select(item => new ArchivedItem
                    {
                        ItemNumber = item.ItemNumber,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        SKU = item.SKU
                    }).ToList()
                }).ToList(),
                TotalItems = TotalEntries,
                LocationCount = LocationsCount
            };

            // Save to file
            var json = System.Text.Json.JsonSerializer.Serialize(archiveData, s_jsonOptions);
            await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

            // Reload archives list
            await LoadArchivesAsync();

            _hasUnarchivedChanges = false; // Data is now archived

            StatusMessage = $"Archived as '{result.Name}'";
            _logger?.LogInformation("Archived allocation data: {Name} ({Count} items)", result.Name, TotalEntries);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Archive failed: {ex.Message}";
            _logger?.LogError(ex, "Failed to archive allocation data");
        }
    }
    private async Task LoadArchivesAsync()
    {
        try
        {
            Archives.Clear();
            var archivePath = GetArchivePath();

            if (!Directory.Exists(archivePath))
            {
                return;
            }

            var files = Directory.GetFiles(archivePath, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f));

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var data = System.Text.Json.JsonSerializer.Deserialize<ArchiveData>(json);
                    if (data != null)
                    {
                        // Capture file path in local variable for correct closure binding
                        var archiveFilePath = file;
                        Archives.Add(new ArchiveViewModel
                        {
                            Name = data.Name,
                            Notes = data.Notes,
                            ArchivedAt = data.ArchivedAt,
                            TotalItems = data.TotalItems,
                            LocationCount = data.LocationCount,
                            FilePath = archiveFilePath,
                            LoadCommand = new AsyncRelayCommand(async () => await LoadArchiveAsync(archiveFilePath)),
                            DeleteCommand = new AsyncRelayCommand(async () => await DeleteArchiveAsync(archiveFilePath))
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load archive file: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load archives");
        }
    }
    private async Task LoadArchiveAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var data = System.Text.Json.JsonSerializer.Deserialize<ArchiveData>(json);

            if (data == null)
            {
                StatusMessage = "Invalid archive file";
                return;
            }

            // Clear current data
            LocationAllocations.Clear();
            ItemPool.Clear();

            // Load archived data
            foreach (var loc in data.Locations)
            {
                var location = new LocationAllocation
                {
                    Location = loc.Location,
                    LocationName = loc.LocationName
                };

                foreach (var item in loc.Items)
                {
                    location.Items.Add(new ItemAllocation
                    {
                        ItemNumber = item.ItemNumber,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        SKU = item.SKU
                    });
                }

                LocationAllocations.Add(location);
            }

            // Update UI
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
            OnPropertyChanged(nameof(HasNoData));
            OnPropertyChanged(nameof(HasData));
            RefreshItemTotals();

            IsArchivePanelOpen = false;
            _hasUnarchivedChanges = false; // Loaded data is already archived
            StatusMessage = $"Loaded archive: {data.Name}";
            _logger?.LogInformation("Loaded archive: {Name}", data.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load archive: {ex.Message}";
            _logger?.LogError(ex, "Failed to load archive: {FilePath}", filePath);
        }
    }
    private async Task DeleteArchiveAsync(string filePath)
    {
        try
        {
            var confirm = new SOUP.Views.AllocationBuddy.ConfirmDialog();
            confirm.SetMessage("Delete this archive? This action cannot be undone.");
            var ok = await _dialogService.ShowContentDialogAsync<bool?>(confirm);
            if (ok != true) return;

            File.Delete(filePath);
            await LoadArchivesAsync();
            StatusMessage = "Archive deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete archive: {ex.Message}";
            _logger?.LogError(ex, "Failed to delete archive: {FilePath}", filePath);
        }
    }

    private static string GetArchivePath()
    {
        return Path.Combine(Core.AppPaths.AllocationBuddyDir, "Archives");
    }

    /// <summary>
    /// Automatically archives the current data if there are unarchived changes.
    /// Called before imports and clears to preserve data.
    /// </summary>
    private async Task AutoArchiveIfNeededAsync()
    {
        // Only auto-archive if there's data and it has been modified
        if (LocationAllocations.Count == 0 || !_hasUnarchivedChanges)
        {
            return;
        }

        await SaveCurrentDataAsync("Auto-Archive", "Automatically saved before new import or clear");
    }

    /// <summary>
    /// Archives the current data on application shutdown.
    /// Called from App.OnExit to ensure data is saved before closing.
    /// </summary>
    public async Task ArchiveOnShutdownAsync()
    {
        if (LocationAllocations.Count == 0)
        {
            return;
        }

        // Use imported file name if available, otherwise generic session save
        var sessionName = !string.IsNullOrEmpty(_lastImportedFileName)
            ? $"Session_{_lastImportedFileName}"
            : "Session-Save";

        await SaveCurrentDataAsync(sessionName, "Automatically saved when application closed");
    }
    public async Task LoadMostRecentArchiveAsync()
    {
        try
        {
            var archivePath = GetArchivePath();
            if (!Directory.Exists(archivePath)) return;

            // Find the most recent Session-Save or Auto-Archive
            var files = Directory.GetFiles(archivePath, "*.json")
                .Select(f => new { Path = f, Info = new FileInfo(f) })
                .OrderByDescending(f => f.Info.LastWriteTime)
                .FirstOrDefault();

            if (files == null) return;

            // Load the archive silently
            var json = await File.ReadAllTextAsync(files.Path);
            var data = System.Text.Json.JsonSerializer.Deserialize<ArchiveData>(json);

            if (data == null || data.Locations.Count == 0) return;

            // Clear current data
            LocationAllocations.Clear();
            ItemPool.Clear();

            // Load archived data
            foreach (var loc in data.Locations)
            {
                var location = new LocationAllocation
                {
                    Location = loc.Location,
                    LocationName = loc.LocationName
                };

                foreach (var item in loc.Items)
                {
                    location.Items.Add(new ItemAllocation
                    {
                        ItemNumber = item.ItemNumber,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        SKU = item.SKU
                    });
                }

                LocationAllocations.Add(location);
            }

            // Update UI
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
            OnPropertyChanged(nameof(HasNoData));
            OnPropertyChanged(nameof(HasData));
            RefreshItemTotals();

            _hasUnarchivedChanges = false; // Loaded data is already archived

            // Extract the original file name from session archives (Session_filename format)
            var archiveFileName = Path.GetFileNameWithoutExtension(files.Path);
            if (archiveFileName.Contains("_Session_"))
            {
                var sessionIndex = archiveFileName.IndexOf("_Session_", StringComparison.Ordinal);
                _lastImportedFileName = archiveFileName[(sessionIndex + 9)..]; // Skip "_Session_"
            }

            StatusMessage = $"Restored {data.Locations.Count} locations from previous session";
            _logger?.LogInformation("Loaded most recent archive: {Name}", data.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load most recent archive");
            // Silent failure - don't interrupt startup
        }
    }
    private async Task SaveCurrentDataAsync(string prefix, string notes)
    {
        try
        {
            var archivePath = GetArchivePath();
            Directory.CreateDirectory(archivePath);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{timestamp}_{prefix}.json";
            var filePath = Path.Combine(archivePath, fileName);

            // Create archive data
            var archiveData = new ArchiveData
            {
                Name = $"{prefix} {DateTime.Now:MMM d, yyyy h:mm tt}",
                Notes = notes,
                ArchivedAt = DateTime.Now,
                Locations = LocationAllocations.Select(loc => new ArchivedLocation
                {
                    Location = loc.Location,
                    LocationName = loc.LocationName,
                    Items = loc.Items.Select(item => new ArchivedItem
                    {
                        ItemNumber = item.ItemNumber,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        SKU = item.SKU
                    }).ToList()
                }).ToList(),
                TotalItems = TotalEntries,
                LocationCount = LocationsCount
            };

            // Save to file
            var json = System.Text.Json.JsonSerializer.Serialize(archiveData, s_jsonOptions);
            await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

            _hasUnarchivedChanges = false;
            _logger?.LogInformation("{Prefix} allocation data ({Count} items)", prefix, TotalEntries);

            // Reload archives list (only if not shutting down)
            if (prefix != "Session-Save")
            {
                await LoadArchivesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save allocation data: {Prefix}", prefix);
            // Don't interrupt the user's workflow if save fails
        }
    }
    private void MarkAsModified()
    {
        _hasUnarchivedChanges = true;
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Unsubscribe from settings changes
                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged -= OnSettingsChanged;
                }

                // Dispose dictionary service
                _dictionaryService?.Dispose();
            }
            _disposed = true;
        }
    }


    /// <summary>
    /// Represents a store's allocation for a specific item (used in item-sorted view).
    /// UI-specific - includes Item reference for command binding.
    /// </summary>
    public class StoreAllocationEntry
    {
        public string StoreCode { get; set; } = string.Empty;
        public string? StoreName { get; set; }
        public int Quantity { get; set; }

        /// <summary>Reference to the actual ItemAllocation object for commands.</summary>
        public ItemAllocation? Item { get; set; }

        public string DisplayStore => string.IsNullOrWhiteSpace(StoreName)
            ? StoreCode
            : $"{StoreCode} - {StoreName}";
    }

    /// <summary>
    /// View model for an item with its store allocations (item-sorted view).
    /// UI-specific - uses StoreAllocationEntry which has Item references for commands.
    /// </summary>
    public class ItemAllocationView
    {
        public string ItemNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public ObservableCollection<StoreAllocationEntry> StoreAllocations { get; set; } = new();

        public string DisplayItem => string.IsNullOrWhiteSpace(Description)
            ? ItemNumber
            : $"{ItemNumber} - {Description}";

        public int StoreCount => StoreAllocations.Count;
    }
    public class ArchiveDialogResult
    {
        public string Name { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
    public class ArchiveViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime ArchivedAt { get; set; }
        public int TotalItems { get; set; }
        public int LocationCount { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public IAsyncRelayCommand? LoadCommand { get; set; }
        public IAsyncRelayCommand? DeleteCommand { get; set; }

        public string DisplayDate => ArchivedAt.ToString("MMM d, yyyy h:mm tt");
        public string Summary => $"{TotalItems} items • {LocationCount} locations";
    }
}
