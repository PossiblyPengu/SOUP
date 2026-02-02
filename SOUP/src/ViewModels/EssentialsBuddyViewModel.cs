using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.EssentialsBuddy;
using SOUP.Core.Interfaces;
using SOUP.Data;
using SOUP.Infrastructure.Services.Parsers;
using SOUP.Services;
using SOUP.Views;
using SOUP.Views.EssentialsBuddy;

namespace SOUP.ViewModels;
public partial class EssentialsBuddyViewModel : ObservableObject, IDisposable
{

    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private readonly IEssentialsBuddyRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly EssentialsBuddyParser _parser;
    private readonly DialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EssentialsBuddyViewModel>? _logger;
    private readonly Infrastructure.Services.SettingsService? _settingsService;
    private bool _isInitialized;
    // Session file name for persisted session (set when importing a file)
    private string? _sessionFileName;
    [ObservableProperty]
    private ObservableCollection<InventoryItem> _items = new();
    [ObservableProperty]
    private ObservableCollection<InventoryItem> _filteredItems = new();
    [ObservableProperty]
    private InventoryItem? _selectedItem;
    [ObservableProperty]
    private string _searchText = string.Empty;
    [ObservableProperty]
    private string _statusFilter = "All";
    [ObservableProperty]
    private bool _essentialsOnly;
    [ObservableProperty]
    private bool _includeAllPrivateLabel;
    [ObservableProperty]
    private bool _isLoading;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    [NotifyPropertyChangedFor(nameof(EssentialItemsCount))]
    [NotifyPropertyChangedFor(nameof(OutOfStockCount))]
    [NotifyPropertyChangedFor(nameof(LowStockCount))]
    private bool _hasNoData = true;
    public bool HasData => !HasNoData;
    public int EssentialItemsCount => Items.Count(i => i.IsEssential);
    public int OutOfStockCount => Items.Count(i => i.Status == InventoryStatus.OutOfStock);
    public int LowStockCount => Items.Count(i => i.Status == InventoryStatus.Low);
    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "All",
        "No Stock",
        "Low Stock",
        "Sufficient"
    };
    public EssentialsBuddyViewModel(IEssentialsBuddyRepository repository, IFileImportExportService fileService, DialogService dialogService, IServiceProvider serviceProvider, ILogger<EssentialsBuddyViewModel>? logger = null)
    {
        (_repository, _fileService, _parser, _dialogService, _serviceProvider, _logger, _settingsService) = (repository, fileService, new EssentialsBuddyParser(null), dialogService, serviceProvider, logger, serviceProvider.GetService<Infrastructure.Services.SettingsService>());
        if (_settingsService != null) _settingsService.SettingsChanged += OnSettingsChanged;
    }
    private void OnSettingsChanged(object? sender, Infrastructure.Services.SettingsChangedEventArgs e)
    {
        if (e.AppName == "EssentialsBuddy")
        {
            _ = LoadAndApplySettingsAsync();
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        // Load and apply settings for stock thresholds
        await LoadAndApplySettingsAsync();
        await LoadItems();
    }

    [RelayCommand]
    public async Task SaveSnapshotAsync()
    {
        try
        {
            var path = GetSnapshotFilePath();
            var snaps = Items.Select(i => new InventoryItemSnapshot(i.ItemNumber, i.QuantityOnHand, i.BinCode, i.LastUpdated)).ToList();
            var json = JsonSerializer.Serialize(snaps, s_jsonOptions);
            await File.WriteAllTextAsync(path, json);
            StatusMessage = $"Saved snapshot ({snaps.Count} items)";
            _logger?.LogInformation("Saved EssentialsBuddy snapshot to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save snapshot");
            StatusMessage = "Failed to save snapshot";
        }
    }

    [RelayCommand]
    public async Task CompareWithPreviousRunAsync()
    {
        try
        {
            var path = GetSnapshotFilePath();
            if (!File.Exists(path))
            {
                StatusMessage = "No previous snapshot to compare";
                return;
            }

            var prevJson = await File.ReadAllTextAsync(path);
            var prev = JsonSerializer.Deserialize<List<InventoryItemSnapshot>>(prevJson, s_jsonOptions) ?? new List<InventoryItemSnapshot>();

            var prevMap = prev.ToDictionary(p => p.ItemNumber, StringComparer.OrdinalIgnoreCase);
            var currMap = Items.ToDictionary(i => i.ItemNumber, StringComparer.OrdinalIgnoreCase);

            var added = currMap.Keys.Except(prevMap.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            var removed = prevMap.Keys.Except(currMap.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            var changed = new List<string>();

            foreach (var key in currMap.Keys.Intersect(prevMap.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var currQ = currMap[key].QuantityOnHand;
                var prevQ = prevMap[key].QuantityOnHand;
                if (currQ != prevQ)
                {
                    changed.Add($"{key}: {prevQ} -> {currQ}");
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Compare results: Added={added.Count}, Removed={removed.Count}, QuantityChanged={changed.Count}");
            if (added.Count > 0)
            {
                sb.AppendLine("\nAdded:");
                foreach (var a in added) sb.AppendLine($" + {a}");
            }
            if (removed.Count > 0)
            {
                sb.AppendLine("\nRemoved:");
                foreach (var r in removed) sb.AppendLine($" - {r}");
            }
            if (changed.Count > 0)
            {
                sb.AppendLine("\nQuantity changes:");
                foreach (var c in changed) sb.AppendLine($" * {c}");
            }

            // Write details to a results file for review
            var outDir = Path.GetDirectoryName(path) ?? Path.GetTempPath();
            var outFile = Path.Combine(outDir, $"essentials_compare_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllTextAsync(outFile, sb.ToString());

            StatusMessage = $"Compared to previous run: +{added.Count} / -{removed.Count} / Δ{changed.Count} (details: {outFile})";
            _logger?.LogInformation("Essentials compare written to {OutFile}", outFile);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to compare snapshots");
            StatusMessage = "Compare failed";
        }
    }
    private async Task LoadAndApplySettingsAsync()
    {
        try
        {
            var svc = _serviceProvider.GetService<Infrastructure.Services.SettingsService>();
            if (svc != null)
            {
                var s = await svc.LoadSettingsAsync<Core.Entities.Settings.EssentialsBuddySettings>("EssentialsBuddy");
                (InventoryItem.GlobalLowStockThreshold, InventoryItem.GlobalSufficientThreshold, StatusFilter, EssentialsOnly) = (s.LowStockThreshold, s.SufficientThreshold, s.DefaultStatusFilter, s.DefaultEssentialsOnly);
                _logger?.LogInformation("Settings: Low={L}, Sufficient={S}, Filter={F}, EO={E}", s.LowStockThreshold, s.SufficientThreshold, s.DefaultStatusFilter, s.DefaultEssentialsOnly);
            }
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "Settings load failed"); }
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
    public event Action? FocusSearchRequested;

    // Snapshot DTO for lightweight persistence between runs
    private record InventoryItemSnapshot(string ItemNumber, int QuantityOnHand, string? BinCode, DateTime? LastUpdated);

    private string GetSnapshotFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "SOUP", "EssentialsBuddy");
        try { Directory.CreateDirectory(dir); } catch (Exception ex) { _logger?.LogWarning(ex, "Failed to create snapshot directory"); }
        return Path.Combine(dir, "last_snapshot.json");
    }

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
            (IsLoading, StatusMessage) = (true, "Loading inventory items...");
            var active = (await _repository.GetAllAsync()).Where(i => !i.IsDeleted).OrderBy(i => i.ItemNumber);
            Items.Clear();
            foreach (var item in active) Items.Add(item);
            ApplyFilters();
            (HasNoData, StatusMessage) = (Items.Count == 0, $"Loaded {Items.Count} items");
            _logger?.LogInformation("Loaded {Count} items", Items.Count);
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; _logger?.LogError(ex, "Load exception"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ImportFromExcel() => await ImportFileAsync("xlsx", "Excel", (f, pl) => _parser.ParseExcelAsync(f, 100, pl));

    [RelayCommand]
    private async Task ImportFromCsv() => await ImportFileAsync("csv", "CSV", (f, pl) => _parser.ParseCsvAsync(f, 100, pl));
    private async Task ImportFileAsync(string ext, string fmt, Func<string, bool, Task<Core.Common.Result<IReadOnlyList<InventoryItem>>>> parse)
    {
        _logger?.LogInformation("Import from {Format} clicked, IncludeAllPrivateLabel={PL}", fmt, IncludeAllPrivateLabel);
        try
        {
            var files = await _dialogService.ShowOpenFileDialogAsync($"Select {fmt} file to import", $"{fmt} Files", ext);
            if (files == null || files.Length == 0) return;
            IsLoading = true;
            StatusMessage = $"Importing from {fmt} (filtering {(IncludeAllPrivateLabel ? "9-90* bins + all PL" : "9-90* bins")})...";
            var result = await parse(files[0], IncludeAllPrivateLabel);
            if (result.IsSuccess && result.Value != null)
            {
                var items = result.Value.ToList();
                var unmatched = MatchItemsAgainstDictionary(items);
                foreach (var item in await _repository.GetAllAsync()) await _repository.DeleteAsync(item.Id);
                foreach (var item in items) await _repository.AddAsync(item);
                await LoadItems();
                try
                {
                    _sessionFileName = $"{Path.GetFileNameWithoutExtension(files[0])}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    Directory.CreateDirectory(GetDataPath());
                    await File.WriteAllTextAsync(GetLastSessionInfoFilePath(), _sessionFileName);
                    await SaveDataOnShutdownAsync();
                    try { await SaveSnapshotAsync(); } catch (Exception ex) { _logger?.LogWarning(ex, "Snapshot save failed"); }
                }
                catch (Exception ex) { _logger?.LogWarning(ex, "Session persist failed"); }
                var (essential, matched, pl) = (items.Count(i => i.IsEssential), items.Count(i => i.DictionaryMatched), items.Count(i => i.IsPrivateLabel));
                StatusMessage = $"Imported {items.Count} items ({matched} matched, {essential} essentials, {pl} PL)";
                _logger?.LogInformation("Imported {Count} items, {Matched} matched, {Essentials} essentials, {PL} PL", items.Count, matched, essential, pl);
                if (unmatched.Count > 0) { IsLoading = false; await PromptToAddUnmatchedItems(unmatched); }
            }
            else
            {
                StatusMessage = $"Import failed: {result.ErrorMessage}";
                _logger?.LogError("Import failed: {Error}", result.ErrorMessage);
            }
            IsLoading = false;
        }
        catch (Exception ex)
        {
            (StatusMessage, IsLoading) = ($"Import error: {ex.Message}", false);
            _logger?.LogError(ex, "Import exception");
        }
    }

    [RelayCommand]
    private async Task EditItem()
    {
        if (SelectedItem == null) { StatusMessage = "Select an item to edit"; return; }
        _logger?.LogInformation("Edit item: {ItemNumber}", SelectedItem.ItemNumber);
        try
        {
            var vm = new InventoryItemDialogViewModel();
            vm.InitializeForEdit(SelectedItem);
            var result = await _dialogService.ShowContentDialogAsync<InventoryItem?>(new InventoryItemDialog { DataContext = vm });
            if (result != null)
            {
                var updated = await _repository.UpdateAsync(result);
                var idx = Items.IndexOf(SelectedItem);
                if (idx >= 0) Items[idx] = updated;
                ApplyFilters();
                StatusMessage = $"Updated {updated.ItemNumber}";
                _logger?.LogInformation("Updated {ItemNumber}", updated.ItemNumber);
            }
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; _logger?.LogError(ex, "Edit exception"); }
    }

    [RelayCommand]
    private async Task DeleteItem()
    {
        if (SelectedItem == null) { StatusMessage = "Select an item to delete"; return; }
        if (System.Windows.MessageBox.Show($"Delete {SelectedItem.ItemNumber}?", "Confirm", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            var num = SelectedItem.ItemNumber;
            if (await _repository.DeleteAsync(SelectedItem.Id))
            {
                Items.Remove(SelectedItem);
                ApplyFilters();
                (StatusMessage, SelectedItem) = ($"Deleted {num}", null);
                _logger?.LogInformation("Deleted {ItemNumber}", num);
            }
            else { StatusMessage = "Delete failed: Item not found"; _logger?.LogError("Delete failed: {ItemNumber}", num); }
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; _logger?.LogError(ex, "Delete exception"); }
    }

    [RelayCommand]
    private async Task ExportToExcel() => await ExportFileAsync("xlsx", "Excel", (items, path) => _fileService.ExportToExcelAsync(items, path));

    [RelayCommand]
    private async Task ExportToCsv() => await ExportFileAsync("csv", "CSV", (items, path) => _fileService.ExportToCsvAsync(items, path));
    private async Task ExportFileAsync(string ext, string fmt, Func<List<InventoryItem>, string, Task<Core.Common.Result>> export)
    {
        try
        {
            if (Items.Count == 0) { StatusMessage = "No items to export"; return; }
            var filePath = await _dialogService.ShowSaveFileDialogAsync($"Export to {fmt}", $"EssentialsBuddy_Export_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}", $"{fmt} Files (*.{ext})|*.{ext}|All Files (*.*)|*.*");
            if (string.IsNullOrEmpty(filePath)) { StatusMessage = "Export cancelled"; return; }
            (IsLoading, StatusMessage) = (true, $"Exporting to {fmt}...");
            var result = await export(Items.ToList(), filePath);
            if (result.IsSuccess)
            {
                StatusMessage = $"Exported {Items.Count} item(s)";
                _logger?.LogInformation("Exported {Count} items to {Format}", Items.Count, fmt);
                _dialogService.ShowExportSuccessDialog(Path.GetFileName(filePath), filePath, Items.Count);
                try { await SaveSnapshotAsync(); } catch (Exception ex) { _logger?.LogWarning(ex, "Snapshot save failed"); }
            }
            else
            {
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                _logger?.LogError("Export failed: {Error}", result.ErrorMessage);
                _dialogService.ShowExportErrorDialog(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            _logger?.LogError(ex, "Export exception");
            _dialogService.ShowExportErrorDialog(ex.Message);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ClearData()
    {
        try
        {
            await _repository.DeleteAllAsync();
            (Items, FilteredItems, SearchText, StatusFilter, HasNoData) = (new(), new(), string.Empty, "All", true);
            DeleteSessionData();
            StatusMessage = "All data cleared";
            _logger?.LogInformation("Cleared all data");
        }
        catch (Exception ex) { StatusMessage = $"Clear error: {ex.Message}"; _logger?.LogError(ex, "Clear exception"); }
    }
    private void DeleteSessionData()
    {
        try
        {
            var dataPath = GetDataPath();
            var lastInfoPath = GetLastSessionInfoFilePath();

            // Delete named session file if present
            if (!string.IsNullOrEmpty(_sessionFileName))
            {
                var namedPath = Path.Combine(dataPath, _sessionFileName);
                if (File.Exists(namedPath))
                {
                    File.Delete(namedPath);
                    _logger?.LogInformation("Deleted session data file: {FilePath}", namedPath);
                }
            }

            // Delete default session-data.json as well
            var defaultPath = Path.Combine(dataPath, "session-data.json");
            if (File.Exists(defaultPath))
            {
                File.Delete(defaultPath);
                _logger?.LogInformation("Deleted session data file: {FilePath}", defaultPath);
            }

            // Remove last-session info file
            if (File.Exists(lastInfoPath))
            {
                File.Delete(lastInfoPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete session data file");
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnStatusFilterChanged(string value) => ApplyFilters();
    partial void OnEssentialsOnlyChanged(bool value) => ApplyFilters();

    // PrivateLabelOnly removed: private-label filtering is no longer exposed in the UI

    private void ApplyFilters()
    {
        IEnumerable<InventoryItem> query = Items;
        query = query.Where(i => i.IsEssential || i.QuantityOnHand > 0 || (i.IsPrivateLabel && (i.BinCode?.StartsWith("9-90", StringComparison.OrdinalIgnoreCase) ?? false)));
        if (EssentialsOnly) query = query.Where(i => i.IsEssential);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText;
            query = query.Where(i => i.ItemNumber.Contains(s, StringComparison.OrdinalIgnoreCase) || i.Description.Contains(s, StringComparison.OrdinalIgnoreCase) || (i.DictionaryDescription?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) || (i.BinCode?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) || (i.Category?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (StatusFilter != "All")
        {
            var en = StatusFilter switch { "No Stock" => "OutOfStock", "Out of Stock" => "OutOfStock", "Low Stock" => "Low", "Low" => "Low", "Sufficient" => "Sufficient", _ => StatusFilter };
            if (Enum.TryParse<InventoryStatus>(en, out var st)) query = query.Where(i => i.Status == st);
        }
        var results = query.OrderBy(i => i.StatusSortOrder).ThenBy(i => i.ItemNumber).ToList();
        FilteredItems.Clear();
        foreach (var item in results) FilteredItems.Add(item);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var win = new UnifiedSettingsWindow(_serviceProvider.GetRequiredService<UnifiedSettingsViewModel>(), "essentials");
            if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mw) win.Owner = mw;
            win.Show();
        }
        catch (Exception ex) { _logger?.LogError(ex, "Settings open failed"); StatusMessage = "Failed to open settings"; }
    }
    private static string GetDataPath() => Core.AppPaths.EssentialsBuddyDir;

    private static string GetDataFilePath() => Path.Combine(GetDataPath(), "session-data.json");

    private static string GetLastSessionInfoFilePath() => Path.Combine(GetDataPath(), "last-session.txt");
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

            // Use the session filename if set (created on import), otherwise fall back to default
            var sessionFileName = _sessionFileName ?? "session-data.json";
            var sessionPath = Path.Combine(dataPath, sessionFileName);

            await File.WriteAllTextAsync(sessionPath, json).ConfigureAwait(false);

            // Persist the last session filename for next startup
            if (!string.IsNullOrEmpty(_sessionFileName))
            {
                await File.WriteAllTextAsync(GetLastSessionInfoFilePath(), _sessionFileName).ConfigureAwait(false);
            }

            _logger?.LogInformation("Saved EssentialsBuddy data: {Count} items to {Path}", Items.Count, sessionPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save EssentialsBuddy data");
        }
    }
    public async Task LoadPersistedDataAsync()
    {
        try
        {
            var dataPath = GetDataPath();
            Directory.CreateDirectory(dataPath);

            // Prefer the last used session file if present
            var lastInfoPath = GetLastSessionInfoFilePath();
            string filePath = GetDataFilePath();
            if (File.Exists(lastInfoPath))
            {
                try
                {
                    var lastName = await File.ReadAllTextAsync(lastInfoPath).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(lastName))
                    {
                        var candidate = Path.Combine(dataPath, lastName.Trim());
                        if (File.Exists(candidate)) filePath = candidate;
                    }
                }
                catch { /* ignore and fallback */ }
            }

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
    private bool _disposed;
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
            // Unsubscribe from settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }

            // Dispose managed resources
            (_repository as IDisposable)?.Dispose();
        }
        _disposed = true;
    }
}
