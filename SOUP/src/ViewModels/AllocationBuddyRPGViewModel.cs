using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SOUP.Core.Entities.AllocationBuddy;
using SOUP.Infrastructure.Services.Parsers;
using SOUP.Helpers;
using SOUP.Services;
using SOUP.Data;
using SOUP.Core.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;

namespace SOUP.ViewModels;

/// <summary>
/// ViewModel for the Allocation Buddy module, managing inventory allocation across store locations.
/// </summary>
/// <remarks>
/// <para>
/// This ViewModel provides functionality for:
/// <list type="bullet">
///   <item>Importing allocation data from Excel, CSV, or clipboard</item>
///   <item>Managing item allocations across multiple store locations</item>
///   <item>Moving items between locations and an item pool</item>
///   <item>Exporting allocation data to Excel or CSV</item>
///   <item>Auto-archiving data to preserve state between sessions</item>
/// </list>
/// </para>
/// <para>
/// Data is automatically archived before imports and when the application closes,
/// ensuring no data loss between sessions.
/// </para>
/// </remarks>
public partial class AllocationBuddyRPGViewModel : ObservableObject, IDisposable
{
    #region Private Fields

    private readonly AllocationBuddyParser _parser;
    private readonly DialogService _dialogService;
    private readonly ILogger<AllocationBuddyRPGViewModel>? _logger;

    /// <summary>
    /// Thread-safe lookup dictionary for O(1) item access by item number.
    /// </summary>
    private ConcurrentDictionary<string, DictionaryItem> _itemLookupByNumber = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Thread-safe lookup dictionary for O(1) item access by SKU.
    /// </summary>
    private ConcurrentDictionary<string, DictionaryItem> _itemLookupBySku = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cancellation token source for background dictionary loading operations.
    /// </summary>
    private CancellationTokenSource? _loadDictionariesCts;

    /// <summary>
    /// Tracks whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Duration in milliseconds for the visual flash effect when items are updated.
    /// </summary>
    private const int UpdateFlashDurationMs = 300;

    /// <summary>
    /// Maximum clipboard text length (10MB) to prevent denial-of-service attacks.
    /// </summary>
    private const int MaxClipboardTextLength = 10_000_000;
    
    /// <summary>
    /// Tracks whether current data has unsaved changes that need archiving.
    /// </summary>
    private bool _hasUnarchivedChanges;

    /// <summary>
    /// Buffer storing the last deactivation operation for undo functionality.
    /// </summary>
    private DeactivationRecord? _lastDeactivation;

    #endregion

    #region Observable Collections

    /// <summary>
    /// Gets the collection of location allocations, each containing items allocated to that location.
    /// </summary>
    public ObservableCollection<LocationAllocation> LocationAllocations { get; } = new();
    
    /// <summary>
    /// Gets the pool of unallocated items that can be moved to locations.
    /// </summary>
    public ObservableCollection<ItemAllocation> ItemPool { get; } = new();
    
    /// <summary>
    /// Gets the results of the last file import operation for display to the user.
    /// </summary>
    public ObservableCollection<FileImportResult> FileImportResults { get; } = new();

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the current status message displayed to the user.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Gets or sets the search text used to filter displayed locations and items.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Gets or sets the text pasted by the user for import from the welcome screen.
    /// </summary>
    [ObservableProperty]
    private string _pasteText = string.Empty;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets a value indicating whether there is no data loaded (displays welcome screen).
    /// </summary>
    public bool HasNoData => LocationAllocations.Count == 0 && ItemPool.Count == 0;
    
    /// <summary>
    /// Gets a value indicating whether data is loaded (displays main interface).
    /// </summary>
    public bool HasData => !HasNoData;

    /// <summary>
    /// Gets the total quantity of all items across all locations.
    /// </summary>
    public int TotalEntries => LocationAllocations.Sum(l => l.Items.Sum(i => i.Quantity));

    /// <summary>
    /// Gets the number of locations with allocations.
    /// </summary>
    public int LocationsCount => LocationAllocations.Count;

    /// <summary>
    /// Gets the number of items in the unallocated pool.
    /// </summary>
    public int ItemPoolCount => ItemPool.Count;

    /// <summary>
    /// Gets locations filtered by the current search text.
    /// </summary>
    /// <remarks>
    /// Filters by location code, location name, item number, and item description.
    /// </remarks>
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

    #endregion

    #region Item Totals

    /// <summary>
    /// Gets a summary of total quantities per unique item across all locations.
    /// </summary>
    public ObservableCollection<ItemTotalSummary> ItemTotals { get; } = new();

    /// <summary>
    /// Current sort mode for Item Totals
    /// </summary>
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

    /// <summary>
    /// Recalculates the ItemTotals collection from all locations.
    /// </summary>
    private void RefreshItemTotals()
    {
        // Get items from locations (allocated)
        var locationItems = LocationAllocations.SelectMany(l => l.Items).ToList();
        
        // Build lookup for pool quantities by item number (case-insensitive)
        var poolByItem = ItemPool
            .GroupBy(p => p.ItemNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key, 
                g => (Quantity: g.Sum(p => p.Quantity), Description: g.First().Description),
                StringComparer.OrdinalIgnoreCase);
        
        // Build lookup for location quantities by item number (case-insensitive)
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

        // Apply sorting based on current mode
        IEnumerable<ItemTotalSummary> sorted = _itemTotalsSortMode switch
        {
            "qty-asc" => grouped.OrderBy(t => t.TotalQuantity),
            "qty-desc" => grouped.OrderByDescending(t => t.TotalQuantity),
            "name-asc" => grouped.OrderBy(t => t.Description),
            "name-desc" => grouped.OrderByDescending(t => t.Description),
            "item-asc" => grouped.OrderBy(t => t.ItemNumber),
            "item-desc" => grouped.OrderByDescending(t => t.ItemNumber),
            _ => grouped.OrderByDescending(t => t.TotalQuantity)
        };

        ItemTotals.Clear();
        foreach (var t in sorted)
        {
            ItemTotals.Add(t);
        }
        
        // Update each pool item's TotalInLocations for display
        foreach (var poolItem in ItemPool)
        {
            if (locationByItem.TryGetValue(poolItem.ItemNumber, out var locData))
            {
                poolItem.TotalInLocations = locData.Quantity;
            }
            else
            {
                poolItem.TotalInLocations = 0;
            }
        }
        
        OnPropertyChanged(nameof(ItemTotals));
    }

    #endregion

    #region Commands

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
    
    /// <summary>Command to export allocation data to an Excel file.</summary>
    public IAsyncRelayCommand ExportToExcelCommand { get; }
    
    /// <summary>Command to export allocation data to a CSV file.</summary>
    public IAsyncRelayCommand ExportToCsvCommand { get; }
    
    /// <summary>Command to sort item totals by the specified mode.</summary>
    public IRelayCommand<string> SortItemTotalsCommand { get; private set; } = null!;

    #endregion

    #region Archive System

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

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationBuddyRPGViewModel"/> class.
    /// </summary>
    /// <param name="parser">The parser for processing allocation data.</param>
    /// <param name="dialogService">The service for displaying dialogs.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public AllocationBuddyRPGViewModel(AllocationBuddyParser parser, DialogService dialogService, ILogger<AllocationBuddyRPGViewModel>? logger = null)
    {
        _parser = parser;
        _dialogService = dialogService;
        _logger = logger;

        // Initialize import/export commands
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        ImportFilesCommand = new AsyncRelayCommand<string[]?>(async files => { if (files != null) await ImportFilesAsync(files); });
        PasteCommand = new RelayCommand(PasteFromClipboard);
        ImportFromPasteTextCommand = new RelayCommand(ImportFromPasteText);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ClearCommand = new RelayCommand(ClearSearch);
        RemoveOneCommand = new RelayCommand<ItemAllocation?>(item => { if (item != null) RemoveOne(item); });
        AddOneCommand = new RelayCommand<ItemAllocation?>(item => { if (item != null) AddOne(item); });
        MoveFromPoolCommand = new RelayCommand<ItemAllocation?>(item => { if (item != null) MoveFromPool(item); });
        DeactivateStoreCommand = new AsyncRelayCommand<LocationAllocation?>(async loc => { if (loc != null) await DeactivateStoreAsync(loc); });
        UndoDeactivateCommand = new RelayCommand(UndoDeactivate, () => _lastDeactivation != null);
        ClearDataCommand = new AsyncRelayCommand(ClearDataAsync);
        CopyLocationToClipboardCommand = new RelayCommand<LocationAllocation>(CopyLocationToClipboard);
        SortItemTotalsCommand = new RelayCommand<string>(mode => { if (mode != null) ItemTotalsSortMode = mode; });
        ExportToExcelCommand = new AsyncRelayCommand(ExportToExcelAsync);
        ExportToCsvCommand = new AsyncRelayCommand(ExportToCsvAsync);
        
        // Archive commands
        ArchiveCurrentCommand = new AsyncRelayCommand(ArchiveCurrentAsync, () => HasData);
        ViewArchivesCommand = new AsyncRelayCommand(LoadArchivesAsync);

        // Load dictionaries on a background task to avoid blocking the UI during startup
        _ = LoadDictionariesAsync();
        
        // Load archives on startup
        _ = LoadArchivesAsync();
    }

    /// <summary>
    /// Loads dictionaries asynchronously and builds lookup tables for O(1) access
    /// </summary>
    private async Task LoadDictionariesAsync()
    {
        // Cancel any existing load operation
        _loadDictionariesCts?.Cancel();
        _loadDictionariesCts = new CancellationTokenSource();
        var cancellationToken = _loadDictionariesCts.Token;

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Load store dictionary from shared location
                var stores = InternalStoreDictionary.GetStores();
                var mappedStores = stores.Select(s => new StoreEntry { Code = s.Code, Name = s.Name, Rank = s.Rank }).ToList();
                _parser.SetStoreDictionary(mappedStores);

                cancellationToken.ThrowIfCancellationRequested();

                // Load item dictionary from shared location
                var items = InternalItemDictionary.GetItems();
                _parser.SetDictionaryItems(items);

                cancellationToken.ThrowIfCancellationRequested();

                // Build lookup dictionaries for O(1) access
                BuildItemLookupDictionaries(items);

                _logger?.LogInformation("Loaded {Count} dictionary items for AllocationBuddy matching", items.Count);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Dictionary loading was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load dictionaries for AllocationBuddy");
        }
    }

    /// <summary>
    /// Builds thread-safe lookup dictionaries from the item list for O(1) access
    /// </summary>
    private void BuildItemLookupDictionaries(IReadOnlyList<DictionaryItem> items)
    {
        // Create new dictionaries to avoid mutation during reads
        var newByNumber = new ConcurrentDictionary<string, DictionaryItem>(StringComparer.OrdinalIgnoreCase);
        var newBySku = new ConcurrentDictionary<string, DictionaryItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            // Add by item number
            if (!string.IsNullOrEmpty(item.Number))
            {
                newByNumber.TryAdd(item.Number, item);
            }

            // Add by each SKU
            if (item.Skus != null)
            {
                foreach (var sku in item.Skus)
                {
                    if (!string.IsNullOrEmpty(sku))
                    {
                        newBySku.TryAdd(sku, item);
                    }
                }
            }
        }

        // Atomic swap of references (thread-safe due to reference assignment)
        _itemLookupByNumber = newByNumber;
        _itemLookupBySku = newBySku;
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
        var confirm = new SOUP.Views.AllocationBuddy.ConfirmDialog();
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

            // Keep original ItemNumber to match with location items
            var itemNumber = it.ItemNumber;
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
                SetTemporaryUpdateFlag(poolItem);
            }
        }

        // clear the location's items and soft-disable
        loc.Items.Clear();
        loc.IsActive = false;

        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        OnPropertyChanged(nameof(LocationsCount));
        OnPropertyChanged(nameof(FilteredLocationAllocations));
        RefreshItemTotals();
    }

    private async Task ImportAsync()
    {
        try
        {
            _logger?.LogInformation("ImportAsync started");
            
            var files = await _dialogService.ShowOpenFileDialogAsync("Select allocation file", "All Files", "xlsx", "csv");
            
            _logger?.LogInformation("File dialog returned: {Files}", files != null ? string.Join(", ", files) : "null");
            
            if (files == null || files.Length == 0) 
            {
                _logger?.LogInformation("No files selected, returning");
                return;
            }
            
            // Auto-archive existing data before importing new data
            await AutoArchiveIfNeededAsync();
            
            var file = files[0];
            _logger?.LogInformation("Importing file: {File}", file);
            
            StatusMessage = $"Importing {Path.GetFileName(file)}...";
            
            Result<IReadOnlyList<AllocationEntry>> result;
            if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                result = await _parser.ParseCsvAsync(file);
            }
            else
            {
                result = await _parser.ParseExcelAsync(file);
            }

            _logger?.LogInformation("Parse result: Success={Success}, Count={Count}, Error={Error}", 
                result.IsSuccess, result.Value?.Count ?? 0, result.ErrorMessage);

            if (!result.IsSuccess || result.Value == null)
            {
                StatusMessage = $"Import failed: {result.ErrorMessage}";
                _logger?.LogError("Import failed: {Error}", result.ErrorMessage);
                System.Windows.MessageBox.Show($"Import failed: {result.ErrorMessage}", "Import Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            if (result.Value.Count == 0)
            {
                StatusMessage = "No valid entries found. Check file has Store, Item, and Quantity columns.";
                _logger?.LogWarning("No entries found in file");
                System.Windows.MessageBox.Show("No valid entries found.\n\nMake sure the file has columns for Store/Location, Item/Product, and Quantity.", "Import Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            PopulateFromEntries(result.Value);
            MarkAsModified();
            StatusMessage = $"Imported {result.Value.Count} entries from {Path.GetFileName(file)}";
            _logger?.LogInformation("Successfully imported {Count} entries", result.Value.Count);
            
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
            RefreshItemTotals();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in ImportAsync");
            StatusMessage = $"Import error: {ex.Message}";
            System.Windows.MessageBox.Show($"Import error: {ex.Message}", "Import Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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

            // Validate clipboard text length to prevent DoS
            if (clipboardText.Length > MaxClipboardTextLength)
            {
                StatusMessage = $"Clipboard content too large (max {MaxClipboardTextLength / 1_000_000}MB)";
                _logger?.LogWarning("Clipboard text rejected: {Length} bytes exceeds maximum", clipboardText.Length);
                return;
            }

            var result = _parser.ParseFromClipboardText(clipboardText);

            if (!result.IsSuccess || result.Value == null)
            {
                StatusMessage = $"Paste failed: {result.ErrorMessage}";
                return;
            }

            // Auto-archive is async, but for paste we'll fire-and-forget
            _ = AutoArchiveIfNeededAsync();
            
            PopulateFromEntries(result.Value);
            MarkAsModified();
            StatusMessage = $"Pasted {result.Value.Count} entries from clipboard";
            OnPropertyChanged(nameof(LocationsCount));
            OnPropertyChanged(nameof(ItemPoolCount));
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(FilteredLocationAllocations));
            RefreshItemTotals();
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

            // Validate text length to prevent DoS
            if (PasteText.Length > MaxClipboardTextLength)
            {
                StatusMessage = $"Text too large (max {MaxClipboardTextLength / 1_000_000}MB)";
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

    /// <summary>
    /// Import multiple files (used by drag and drop). Accepts full file paths.
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
            await AutoArchiveIfNeededAsync();
            PopulateFromEntries(allEntries);
            MarkAsModified();
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
            RefreshItemTotals();
        }
    }

    /// <summary>
    /// Gets the SKU for an item number using O(1) dictionary lookup
    /// </summary>
    private string? GetSKU(string itemNumber)
    {
        var dictItem = FindDictionaryItem(itemNumber);
        return dictItem?.Skus?.FirstOrDefault();
    }

    /// <summary>
    /// Gets the description for an item number using O(1) dictionary lookup
    /// </summary>
    private string GetDescription(string itemNumber)
    {
        var dictItem = FindDictionaryItem(itemNumber);
        return dictItem?.Description ?? "";
    }

    /// <summary>
    /// Gets the canonical item number using O(1) dictionary lookup
    /// </summary>
    private string GetCanonicalItemNumber(string itemNumber)
    {
        var dictItem = FindDictionaryItem(itemNumber);
        return dictItem?.Number ?? itemNumber;
    }

    /// <summary>
    /// Finds a dictionary item by number or SKU using O(1) lookup
    /// </summary>
    private DictionaryItem? FindDictionaryItem(string itemNumber)
    {
        if (string.IsNullOrEmpty(itemNumber))
            return null;

        // Try lookup by item number first
        if (_itemLookupByNumber.TryGetValue(itemNumber, out var itemByNumber))
            return itemByNumber;

        // Try lookup by SKU
        if (_itemLookupBySku.TryGetValue(itemNumber, out var itemBySku))
            return itemBySku;

        return null;
    }

    /// <summary>
    /// Sets the IsUpdated flag on an item temporarily to trigger a visual flash effect
    /// </summary>
    private static void SetTemporaryUpdateFlag(ItemAllocation item)
    {
        item.IsUpdated = true;
        _ = Task.Delay(UpdateFlashDurationMs).ContinueWith(_ => item.IsUpdated = false);
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
            // Keep original ItemNumber to match with location items
            var itemNumber = target.ItemNumber;
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
            SetTemporaryUpdateFlag(poolItem);
        }
        // mark target updated
        SetTemporaryUpdateFlag(target);
        if (target.Quantity == 0)
            loc.Items.Remove(target);
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        RefreshItemTotals();
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
            SetTemporaryUpdateFlag(target);
        }

        poolItem.Quantity -= 1;
        SetTemporaryUpdateFlag(poolItem);
        if (poolItem.Quantity == 0) ItemPool.Remove(poolItem);
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(ItemPoolCount));
        RefreshItemTotals();
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
        SetTemporaryUpdateFlag(poolItem);
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
        RefreshItemTotals();
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

    #region Archive System

    /// <summary>
    /// Archives the current allocation data with a name and optional notes.
    /// </summary>
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
            var json = System.Text.Json.JsonSerializer.Serialize(archiveData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);

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

    /// <summary>
    /// Loads the list of archives from disk.
    /// </summary>
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
                        Archives.Add(new ArchiveViewModel
                        {
                            Name = data.Name,
                            Notes = data.Notes,
                            ArchivedAt = data.ArchivedAt,
                            TotalItems = data.TotalItems,
                            LocationCount = data.LocationCount,
                            FilePath = file,
                            LoadCommand = new AsyncRelayCommand(async () => await LoadArchiveAsync(file)),
                            DeleteCommand = new AsyncRelayCommand(async () => await DeleteArchiveAsync(file))
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

    /// <summary>
    /// Loads an archived allocation into the current view.
    /// </summary>
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

    /// <summary>
    /// Deletes an archive file.
    /// </summary>
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
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "SOUP", "AllocationBuddy", "Archives");
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

        await SaveCurrentDataAsync("Session-Save", "Automatically saved when application closed");
    }

    /// <summary>
    /// Loads the most recent archive on startup to restore previous session.
    /// </summary>
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
            StatusMessage = $"Restored {data.Locations.Count} locations from previous session";
            _logger?.LogInformation("Loaded most recent archive: {Name}", data.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load most recent archive");
            // Silent failure - don't interrupt startup
        }
    }

    /// <summary>
    /// Saves the current allocation data to an archive file.
    /// </summary>
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
            var json = System.Text.Json.JsonSerializer.Serialize(archiveData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);

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

    /// <summary>
    /// Marks that the current data has been modified and should be auto-archived.
    /// </summary>
    private void MarkAsModified()
    {
        _hasUnarchivedChanges = true;
    }

    #endregion

    /// <summary>
    /// Releases resources used by this ViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _loadDictionariesCts?.Cancel();
                _loadDictionariesCts?.Dispose();
                _loadDictionariesCts = null;
            }
            _disposed = true;
        }
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

        /// <summary>Total quantity of this item across all locations (not including pool).</summary>
        [ObservableProperty]
        private int _totalInLocations;

        // Notify GrandTotal when TotalInLocations changes
        partial void OnTotalInLocationsChanged(int value)
        {
            OnPropertyChanged(nameof(GrandTotal));
        }

        // Notify GrandTotal when Quantity changes
        partial void OnQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(GrandTotal));
        }

        /// <summary>Grand total = pool quantity + all locations.</summary>
        public int GrandTotal => Quantity + TotalInLocations;
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
        /// <summary>Quantity remaining in the item pool (unallocated).</summary>
        public int PoolQuantity { get; set; }
        /// <summary>Total allocated quantity (TotalQuantity - PoolQuantity).</summary>
        public int AllocatedQuantity => TotalQuantity - PoolQuantity;
        /// <summary>Whether this item has any remaining in the pool.</summary>
        public bool HasPoolItems => PoolQuantity > 0;
    }

    #endregion

    #region Archive Data Classes

    /// <summary>
    /// Result from the archive dialog.
    /// </summary>
    public class ArchiveDialogResult
    {
        public string Name { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    /// <summary>
    /// ViewModel for displaying an archive in the list.
    /// </summary>
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

    /// <summary>
    /// Data structure for archived allocation data.
    /// </summary>
    public class ArchiveData
    {
        public string Name { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime ArchivedAt { get; set; }
        public int TotalItems { get; set; }
        public int LocationCount { get; set; }
        public List<ArchivedLocation> Locations { get; set; } = new();
    }

    public class ArchivedLocation
    {
        public string Location { get; set; } = string.Empty;
        public string? LocationName { get; set; }
        public List<ArchivedItem> Items { get; set; } = new();
    }

    public class ArchivedItem
    {
        public string ItemNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? SKU { get; set; }
    }

    #endregion
}
