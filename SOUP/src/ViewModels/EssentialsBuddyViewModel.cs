using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SOUP.Core.Entities.EssentialsBuddy;
using SOUP.Core.Interfaces;
using SOUP.Data;
using SOUP.Services;
using SOUP.Views.EssentialsBuddy;
using SOUP.Views;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.ViewModels;

/// <summary>
/// ViewModel for the Essentials Buddy module, managing essential inventory items and stock levels.
/// </summary>
/// <remarks>
/// <para>
/// This module helps track essential items that should always be in stock, providing:
/// <list type="bullet">
///   <item>Import of inventory data from Excel or CSV files</item>
///   <item>Automatic matching against the item dictionary to identify essential items</item>
///   <item>Filtering by stock status (Normal, Low, Out of Stock, Sufficient)</item>
///   <item>Automatic addition of missing essential items with zero quantity</item>
///   <item>Persistent storage of data between sessions</item>
/// </list>
/// </para>
/// </remarks>
public partial class EssentialsBuddyViewModel : ObservableObject, IDisposable
{
    #region Private Fields

    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    
    private readonly IEssentialsBuddyRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly EssentialsBuddyParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EssentialsBuddyViewModel>? _logger;
    private bool _isInitialized;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the full collection of inventory items.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<InventoryItem> _items = new();

    /// <summary>
    /// Gets or sets the filtered collection of items based on current filters.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<InventoryItem> _filteredItems = new();

    /// <summary>
    /// Gets or sets the currently selected inventory item.
    /// </summary>
    [ObservableProperty]
    private InventoryItem? _selectedItem;

    /// <summary>
    /// Gets or sets the search text for filtering items.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Gets or sets the current status filter (All, Normal, Low, OutOfStock, Sufficient).
    /// </summary>
    [ObservableProperty]
    private string _statusFilter = "All";

    /// <summary>
    /// Gets or sets whether to show only essential items.
    /// </summary>
    [ObservableProperty]
    private bool _essentialsOnly;

    /// <summary>
    /// Gets or sets whether to show only private label items.
    /// </summary>
    [ObservableProperty]
    private bool _privateLabelOnly;

    /// <summary>
    /// Gets or sets whether to include all private label items from any bin during import.
    /// </summary>
    [ObservableProperty]
    private bool _includeAllPrivateLabel;

    /// <summary>
    /// Gets or sets whether data is currently being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Gets or sets the current status message displayed to the user.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Gets or sets whether there is no data loaded.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    [NotifyPropertyChangedFor(nameof(EssentialItemsCount))]
    [NotifyPropertyChangedFor(nameof(OutOfStockCount))]
    [NotifyPropertyChangedFor(nameof(LowStockCount))]
    private bool _hasNoData = true;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets a value indicating whether data is loaded.
    /// </summary>
    public bool HasData => !HasNoData;
    
    /// <summary>
    /// Gets the count of items marked as essential.
    /// </summary>
    public int EssentialItemsCount => Items.Count(i => i.IsEssential);
    
    /// <summary>
    /// Gets the count of items that are out of stock.
    /// </summary>
    public int OutOfStockCount => Items.Count(i => i.Status == InventoryStatus.OutOfStock);
    
    /// <summary>
    /// Gets the count of items with low stock levels.
    /// </summary>
    public int LowStockCount => Items.Count(i => i.Status == InventoryStatus.Low);

    /// <summary>
    /// Gets the available status filter options.
    /// </summary>
    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "All",
        "Normal",
        "Low",
        "OutOfStock",
        "Sufficient"
    };

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="EssentialsBuddyViewModel"/> class.
    /// </summary>
    /// <param name="repository">The repository for persisting inventory data.</param>
    /// <param name="fileService">The service for file import/export operations.</param>
    /// <param name="dialogService">The service for displaying dialogs.</param>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public EssentialsBuddyViewModel(
        IEssentialsBuddyRepository repository,
        IFileImportExportService fileService,
        DialogService dialogService,
        IServiceProvider serviceProvider,
        ILogger<EssentialsBuddyViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _parser = new EssentialsBuddyParser(null);
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        
        // Load and apply settings for stock thresholds
        await LoadAndApplySettingsAsync();
        await LoadItems();
    }

    /// <summary>
    /// Loads settings from the settings service and applies stock thresholds globally.
    /// </summary>
    private async Task LoadAndApplySettingsAsync()
    {
        try
        {
            var settingsService = _serviceProvider.GetService<Infrastructure.Services.SettingsService>();
            if (settingsService != null)
            {
                var settings = await settingsService.LoadSettingsAsync<Core.Entities.Settings.EssentialsBuddySettings>("EssentialsBuddy");
                
                // Apply thresholds to the static properties on InventoryItem
                InventoryItem.GlobalLowStockThreshold = settings.LowStockThreshold;
                InventoryItem.GlobalSufficientThreshold = settings.SufficientThreshold;
                
                _logger?.LogInformation("Applied EssentialsBuddy settings: LowStock={Low}, Sufficient={Sufficient}", 
                    settings.LowStockThreshold, settings.SufficientThreshold);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load EssentialsBuddy settings, using defaults");
        }
    }

    /// <summary>
    /// Match items against the dictionary database to get descriptions and essential status.
    /// Also adds ALL essential items from the dictionary that aren't already in the list.
    /// Returns a list of unmatched items.
    /// </summary>
    private List<InventoryItem> MatchItemsAgainstDictionary(List<InventoryItem> items)
    {
        var matchedCount = 0;
        var essentialCount = 0;
        var privateLabelCount = 0;
        var unmatchedItems = new List<InventoryItem>();
        var existingItemNumbers = new HashSet<string>(items.Select(i => i.ItemNumber));

        foreach (var item in items)
        {
            // Try exact match
            var dictEntity = InternalItemDictionary.GetEntity(item.ItemNumber);
            
            if (dictEntity != null)
            {
                item.DictionaryMatched = true;
                item.DictionaryDescription = dictEntity.Description;
                item.IsEssential = dictEntity.IsEssential;
                item.IsPrivateLabel = dictEntity.IsPrivateLabel;
                item.Tags = dictEntity.Tags ?? new List<string>();
                matchedCount++;
                if (dictEntity.IsEssential) essentialCount++;
                if (dictEntity.IsPrivateLabel) privateLabelCount++;
            }
            else
            {
                item.DictionaryMatched = false;
                unmatchedItems.Add(item);
            }
        }

        // Add ALL essential items from dictionary that aren't already in the list
        var allEssentials = InternalItemDictionary.GetAllEssentialItems();
        var addedEssentialsCount = 0;
        
        foreach (var essential in allEssentials)
        {
            if (!existingItemNumbers.Contains(essential.Number))
            {
                var newItem = new InventoryItem
                {
                    ItemNumber = essential.Number,
                    Description = essential.Description,
                    DictionaryDescription = essential.Description,
                    DictionaryMatched = true,
                    IsEssential = true,
                    IsPrivateLabel = essential.IsPrivateLabel,
                    Tags = essential.Tags ?? new List<string>(),
                    QuantityOnHand = 0,
                    BinCode = "Not in bins"
                };
                items.Add(newItem);
                addedEssentialsCount++;
            }
        }

        _logger?.LogInformation("Dictionary matching: {Matched}/{Total} items matched, {Essentials} marked as essential, {PL} private label, {Unmatched} unmatched, {AddedEssentials} essential items added from dictionary",
            matchedCount, items.Count - addedEssentialsCount, essentialCount, privateLabelCount, unmatchedItems.Count, addedEssentialsCount);
        
        return unmatchedItems;
    }

    /// <summary>
    /// Prompt user to add unmatched items to the dictionary with a detailed dialog
    /// </summary>
    private async Task PromptToAddUnmatchedItems(List<InventoryItem> unmatchedItems)
    {
        if (unmatchedItems.Count == 0)
            return;

        // Show dialog for user to edit items before adding
        var dialog = new Views.EssentialsBuddy.AddToDictionaryDialog(unmatchedItems);
        // Only set owner if MainWindow is visible (don't block widget)
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mainWindow)
        {
            dialog.Owner = mainWindow;
        }
        
        var result = dialog.ShowDialog();
        
        if (result == true && dialog.WasConfirmed)
        {
            var addedCount = 0;
            var essentialCount = 0;
            
            foreach (var item in dialog.Items)
            {
                // Add to dictionary with user-provided description and essential status
                var dictItem = new Infrastructure.Services.Parsers.DictionaryItem
                {
                    Number = item.ItemNumber,
                    Description = !string.IsNullOrEmpty(item.Description) ? item.Description : item.ItemNumber,
                    Skus = new List<string>()
                };
                
                InternalItemDictionary.UpsertItem(dictItem);
                
                // Set essential status
                if (item.IsEssential)
                {
                    InternalItemDictionary.SetEssential(item.ItemNumber, true);
                    essentialCount++;
                }
                
                // Update the item to show it's now matched
                item.DictionaryMatched = true;
                item.DictionaryDescription = dictItem.Description;
                addedCount++;
            }
            
            StatusMessage = $"Added {addedCount} items to dictionary ({essentialCount} essentials)";
            _logger?.LogInformation("Added {Count} items to dictionary, {Essentials} marked as essential", addedCount, essentialCount);
            
            // Refresh the view to show updated match status
            await LoadItems();
        }
    }

    /// <summary>
    /// Event raised when search box should be focused (triggered by Ctrl+F)
    /// </summary>
    public event Action? FocusSearchRequested;

    [RelayCommand]
    private void FocusSearch()
    {
        FocusSearchRequested?.Invoke();
    }

    [RelayCommand]
    private async Task LoadItems()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading inventory items...";

            var allItems = await _repository.GetAllAsync();
            var activeItems = allItems.Where(i => !i.IsDeleted).ToList();

            Items.Clear();
            foreach (var item in activeItems.OrderBy(i => i.ItemNumber))
            {
                Items.Add(item);
            }

            ApplyFilters();
            HasNoData = Items.Count == 0;
            StatusMessage = $"Loaded {Items.Count} items";
            _logger?.LogInformation("Loaded {Count} inventory items", Items.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading items: {ex.Message}";
            _logger?.LogError(ex, "Exception while loading inventory items");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportFromExcel()
    {
        _logger?.LogInformation("Import from Excel clicked - using specialized parser (9-90* bins, aggregation), IncludeAllPrivateLabel={IncludePL}", IncludeAllPrivateLabel);

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select Excel file to import",
                "Excel Files", "xlsx");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                var binFilter = IncludeAllPrivateLabel ? "9-90* bins + all Private Label" : "9-90* bins";
                StatusMessage = $"Importing from Excel (filtering {binFilter})...";

                // Use specialized parser with exact JS logic
                var result = await _parser.ParseExcelAsync(files[0], 100, IncludeAllPrivateLabel);

                if (result.IsSuccess && result.Value != null)
                {
                    // Convert to list so we can add essential items from dictionary
                    var items = result.Value.ToList();
                    
                    // Match against dictionary for descriptions and essential status
                    var unmatchedItems = MatchItemsAgainstDictionary(items);
                    
                    // Clear existing and add new (replacing data like JS version)
                    var existing = await _repository.GetAllAsync();
                    foreach (var item in existing)
                    {
                        await _repository.DeleteAsync(item.Id);
                    }

                    foreach (var item in items)
                    {
                        await _repository.AddAsync(item);
                    }

                    await LoadItems();
                    
                    var essentialCount = items.Count(i => i.IsEssential);
                    var matchedCount = items.Count(i => i.DictionaryMatched);
                    var privateLabelCount = items.Count(i => i.IsPrivateLabel);
                    StatusMessage = $"Imported {items.Count} items ({matchedCount} matched, {essentialCount} essentials, {privateLabelCount} PL)";
                    _logger?.LogInformation("Imported {Count} items from Excel, {Matched} matched dictionary, {Essentials} essentials, {PL} private label", 
                        items.Count, matchedCount, essentialCount, privateLabelCount);
                    
                    // Prompt to add unmatched items to dictionary
                    if (unmatchedItems.Count > 0)
                    {
                        IsLoading = false;
                        await PromptToAddUnmatchedItems(unmatchedItems);
                    }
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                    _logger?.LogError("Failed to import from Excel: {Error}", result.ErrorMessage);
                }

                IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            _logger?.LogError(ex, "Exception during Excel import");
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ImportFromCsv()
    {
        _logger?.LogInformation("Import from CSV clicked - using specialized parser (9-90* bins, aggregation), IncludeAllPrivateLabel={IncludePL}", IncludeAllPrivateLabel);

        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync(
                "Select CSV file to import",
                "CSV Files", "csv");

            if (files != null && files.Length > 0)
            {
                IsLoading = true;
                var binFilter = IncludeAllPrivateLabel ? "9-90* bins + all Private Label" : "9-90* bins";
                StatusMessage = $"Importing from CSV (filtering {binFilter})...";

                // Use specialized parser with exact JS logic
                var result = await _parser.ParseCsvAsync(files[0], 100, IncludeAllPrivateLabel);

                if (result.IsSuccess && result.Value != null)
                {
                    // Convert to list so we can add essential items from dictionary
                    var items = result.Value.ToList();
                    
                    // Match against dictionary for descriptions and essential status
                    var unmatchedItems = MatchItemsAgainstDictionary(items);
                    
                    // Clear existing and add new
                    var existing = await _repository.GetAllAsync();
                    foreach (var item in existing)
                    {
                        await _repository.DeleteAsync(item.Id);
                    }

                    foreach (var item in items)
                    {
                        await _repository.AddAsync(item);
                    }

                    await LoadItems();
                    
                    var essentialCount = items.Count(i => i.IsEssential);
                    var matchedCount = items.Count(i => i.DictionaryMatched);
                    var privateLabelCount = items.Count(i => i.IsPrivateLabel);
                    StatusMessage = $"Imported {items.Count} items ({matchedCount} matched, {essentialCount} essentials, {privateLabelCount} PL)";
                    _logger?.LogInformation("Imported {Count} items from CSV, {Matched} matched dictionary, {Essentials} essentials, {PL} private label", 
                        items.Count, matchedCount, essentialCount, privateLabelCount);
                    
                    // Prompt to add unmatched items to dictionary
                    if (unmatchedItems.Count > 0)
                    {
                        IsLoading = false;
                        await PromptToAddUnmatchedItems(unmatchedItems);
                    }
                }
                else
                {
                    StatusMessage = $"Import failed: {result.ErrorMessage}";
                    _logger?.LogError("Failed to import from CSV: {Error}", result.ErrorMessage);
                }

                IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            _logger?.LogError(ex, "Exception during CSV import");
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task EditItem()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to edit";
            return;
        }

        _logger?.LogInformation("Edit item clicked for {ItemNumber}", SelectedItem.ItemNumber);

        try
        {
            var dialogViewModel = new InventoryItemDialogViewModel();
            dialogViewModel.InitializeForEdit(SelectedItem);

            var dialog = new InventoryItemDialog
            {
                DataContext = dialogViewModel
            };

            var result = await _dialogService.ShowContentDialogAsync<InventoryItem?>(dialog);

            if (result != null)
            {
                var updatedItem = await _repository.UpdateAsync(result);

                // Update the item in the collection
                var index = Items.IndexOf(SelectedItem);
                if (index >= 0)
                {
                    Items[index] = updatedItem;
                }

                ApplyFilters();
                StatusMessage = $"Updated item {updatedItem.ItemNumber}";
                _logger?.LogInformation("Updated item {ItemNumber}", updatedItem.ItemNumber);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating item: {ex.Message}";
            _logger?.LogError(ex, "Exception while updating item");
        }
    }

    [RelayCommand]
    private async Task DeleteItem()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to delete";
            return;
        }

        // Confirmation dialog
        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete item {SelectedItem.ItemNumber}?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            var itemNumber = SelectedItem.ItemNumber;
            var success = await _repository.DeleteAsync(SelectedItem.Id);

            if (success)
            {
                Items.Remove(SelectedItem);
                ApplyFilters();
                StatusMessage = $"Deleted item {itemNumber}";
                _logger?.LogInformation("Deleted inventory item {ItemNumber}", itemNumber);
                SelectedItem = null;
            }
            else
            {
                StatusMessage = $"Error deleting item: Item not found";
                _logger?.LogError("Failed to delete inventory item {ItemNumber}", itemNumber);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger?.LogError(ex, "Exception while deleting inventory item");
        }
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Exporting to Excel...";

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"EssentialsBuddy_Export_{timestamp}.xlsx";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            var result = await _fileService.ExportToExcelAsync(Items.ToList(), filePath);

            if (result.IsSuccess)
            {
                StatusMessage = $"Exported to {fileName}";
                _logger?.LogInformation("Exported {Count} items to Excel", Items.Count);
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                _logger?.LogError("Failed to export to Excel: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _logger?.LogError(ex, "Exception during Excel export");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportToCsv()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Exporting to CSV...";

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"EssentialsBuddy_Export_{timestamp}.csv";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            var result = await _fileService.ExportToCsvAsync(Items.ToList(), filePath);

            if (result.IsSuccess)
            {
                StatusMessage = $"Exported to {fileName}";
                _logger?.LogInformation("Exported {Count} items to CSV", Items.Count);
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                _logger?.LogError("Failed to export to CSV: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _logger?.LogError(ex, "Exception during CSV export");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearData()
    {
        try
        {
            Items.Clear();
            FilteredItems.Clear();
            SearchText = string.Empty;
            StatusFilter = "All";
            StatusMessage = "All data cleared";
            _logger?.LogInformation("Cleared all EssentialsBuddy data");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear error: {ex.Message}";
            _logger?.LogError(ex, "Exception during data clear");
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnStatusFilterChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnEssentialsOnlyChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnPrivateLabelOnlyChanged(bool value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        // Build query - all filtering is deferred until ToList()
        IEnumerable<InventoryItem> query = Items;

        // Determine if an item should be shown based on bin and status
        // - Essential items: always show
        // - PL items in 9-90 bins: always show
        // - PL items NOT in 9-90 bins: only show if PrivateLabelOnly checkbox is checked
        // - Other items with zero quantity: hide
        query = query.Where(i => 
            i.IsEssential || 
            i.QuantityOnHand > 0 ||
            (i.IsPrivateLabel && (i.BinCode?.StartsWith("9-90", StringComparison.OrdinalIgnoreCase) ?? false)) ||
            (i.IsPrivateLabel && PrivateLabelOnly));

        // Essentials filter
        if (EssentialsOnly)
        {
            query = query.Where(i => i.IsEssential);
        }

        // Note: PrivateLabelOnly affects the base filter above - when checked, it includes PL items not in 9-90

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText;
            query = query.Where(i =>
                i.ItemNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.DictionaryDescription?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.BinCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Category?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (StatusFilter != "All" && Enum.TryParse<InventoryStatus>(StatusFilter, out var status))
        {
            query = query.Where(i => i.Status == status);
        }

        // Materialize once, then bulk update collection
        var results = query.OrderBy(i => i.ItemNumber).ToList();
        
        FilteredItems.Clear();
        foreach (var item in results)
        {
            FilteredItems.Add(item);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsViewModel = _serviceProvider.GetRequiredService<UnifiedSettingsViewModel>();
            var settingsWindow = new UnifiedSettingsWindow(settingsViewModel);
            // Only set owner if MainWindow is visible (don't block widget)
            if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mainWindow)
            {
                settingsWindow.Owner = mainWindow;
            }
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open settings window");
            StatusMessage = "Failed to open settings";
        }
    }

    #endregion

    #region Data Persistence

    private static string GetDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "SOUP", "EssentialsBuddy");
    }

    private static string GetDataFilePath() => Path.Combine(GetDataPath(), "session-data.json");

    /// <summary>
    /// Saves current data on application shutdown.
    /// </summary>
    public async Task SaveDataOnShutdownAsync()
    {
        if (Items.Count == 0) return;

        try
        {
            var dataPath = GetDataPath();
            Directory.CreateDirectory(dataPath);

            var data = new EssentialsBuddyData
            {
                SavedAt = DateTime.Now,
                Items = Items.Select(i => new SavedInventoryItem
                {
                    ItemNumber = i.ItemNumber,
                    Upc = i.Upc,
                    Description = i.Description,
                    DictionaryDescription = i.DictionaryDescription,
                    IsEssential = i.IsEssential,
                    DictionaryMatched = i.DictionaryMatched,
                    BinCode = i.BinCode,
                    Location = i.Location,
                    Category = i.Category,
                    QuantityOnHand = i.QuantityOnHand,
                    MinimumThreshold = i.MinimumThreshold,
                    MaximumThreshold = i.MaximumThreshold,
                    UnitCost = i.UnitCost,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            var json = JsonSerializer.Serialize(data, s_jsonOptions);
            await File.WriteAllTextAsync(GetDataFilePath(), json).ConfigureAwait(false);

            _logger?.LogInformation("Saved EssentialsBuddy data: {Count} items", Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save EssentialsBuddy data");
        }
    }

    /// <summary>
    /// Loads persisted data on startup.
    /// </summary>
    public async Task LoadPersistedDataAsync()
    {
        try
        {
            var filePath = GetDataFilePath();
            if (!File.Exists(filePath)) return;

            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<EssentialsBuddyData>(json);

            if (data?.Items == null || data.Items.Count == 0) return;

            Items.Clear();
            foreach (var saved in data.Items)
            {
                Items.Add(new InventoryItem
                {
                    ItemNumber = saved.ItemNumber,
                    Upc = saved.Upc,
                    Description = saved.Description,
                    DictionaryDescription = saved.DictionaryDescription,
                    IsEssential = saved.IsEssential,
                    DictionaryMatched = saved.DictionaryMatched,
                    BinCode = saved.BinCode,
                    Location = saved.Location,
                    Category = saved.Category,
                    QuantityOnHand = saved.QuantityOnHand,
                    MinimumThreshold = saved.MinimumThreshold,
                    MaximumThreshold = saved.MaximumThreshold,
                    UnitCost = saved.UnitCost,
                    UnitPrice = saved.UnitPrice
                });
            }

            ApplyFilters();
            HasNoData = Items.Count == 0;
            StatusMessage = $"Restored {Items.Count} items from previous session";
            _logger?.LogInformation("Loaded EssentialsBuddy persisted data: {Count} items", Items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load EssentialsBuddy persisted data");
        }
    }

    #endregion

    #region Persistence Data Classes

    private sealed class EssentialsBuddyData
    {
        public DateTime SavedAt { get; set; }
        public List<SavedInventoryItem> Items { get; set; } = new();
    }

    private sealed class SavedInventoryItem
    {
        public string ItemNumber { get; set; } = string.Empty;
        public string Upc { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? DictionaryDescription { get; set; }
        public bool IsEssential { get; set; }
        public bool DictionaryMatched { get; set; }
        public string? BinCode { get; set; }
        public string? Location { get; set; }
        public string? Category { get; set; }
        public int QuantityOnHand { get; set; }
        public int? MinimumThreshold { get; set; }
        public int? MaximumThreshold { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? UnitPrice { get; set; }
    }

    #endregion

    #region IDisposable

    private bool _disposed;

    /// <summary>
    /// Releases resources used by the ViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed and unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Dispose managed resources
            (_repository as IDisposable)?.Dispose();
        }
        _disposed = true;
    }

    #endregion
}
