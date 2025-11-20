using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BusinessToolsSuite.Core.Entities.AllocationBuddy;
using BusinessToolsSuite.Core.Interfaces;

namespace BusinessToolsSuite.Features.AllocationBuddy.ViewModels;

/// <summary>
/// ViewModel for Allocation Buddy store allocation management
/// </summary>
public partial class AllocationBuddyViewModel : ObservableObject
{
    private readonly IAllocationBuddyRepository _repository;
    private readonly IFileImportExportService _fileService;
    private readonly ILogger<AllocationBuddyViewModel>? _logger;

    [ObservableProperty]
    private ObservableCollection<AllocationEntry> _entries = new();

    [ObservableProperty]
    private ObservableCollection<AllocationEntry> _filteredEntries = new();

    [ObservableProperty]
    private AllocationEntry? _selectedEntry;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _rankFilter = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<string> RankFilters { get; } = new()
    {
        "All",
        "A",
        "B",
        "C",
        "D"
    };

    public AllocationBuddyViewModel(
        IAllocationBuddyRepository repository,
        IFileImportExportService fileService,
        ILogger<AllocationBuddyViewModel>? logger = null)
    {
        _repository = repository;
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the view model and load data
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadEntries();
    }

    [RelayCommand]
    private async Task LoadEntries()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading allocation entries...";

            var allEntries = await _repository.GetAllAsync();

            // Filter out soft-deleted items
            var activeEntries = allEntries.Where(e => !e.IsDeleted).ToList();

            Entries.Clear();
            foreach (var entry in activeEntries.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber))
            {
                Entries.Add(entry);
            }

            ApplyFilters();
            StatusMessage = $"Loaded {Entries.Count} allocation entries";
            _logger?.LogInformation("Loaded {Count} allocation entries", Entries.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading entries: {ex.Message}";
            _logger?.LogError(ex, "Exception while loading allocation entries");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddEntry()
    {
        // TODO: Open add entry dialog
        _logger?.LogInformation("Add entry clicked");
        StatusMessage = "Add entry dialog coming soon...";
    }

    [RelayCommand]
    private void EditEntry()
    {
        if (SelectedEntry == null)
        {
            StatusMessage = "Please select an entry to edit";
            return;
        }

        // TODO: Open edit entry dialog
        _logger?.LogInformation("Edit entry clicked for {ItemNumber}", SelectedEntry.ItemNumber);
        StatusMessage = "Edit entry dialog coming soon...";
    }

    [RelayCommand]
    private async Task DeleteEntry()
    {
        if (SelectedEntry == null)
        {
            StatusMessage = "Please select an entry to delete";
            return;
        }

        try
        {
            var itemNumber = SelectedEntry.ItemNumber;
            var storeId = SelectedEntry.StoreId;
            var success = await _repository.DeleteAsync(SelectedEntry.Id);

            if (success)
            {
                Entries.Remove(SelectedEntry);
                ApplyFilters();
                StatusMessage = $"Deleted allocation for {itemNumber} at store {storeId}";
                _logger?.LogInformation("Deleted allocation entry {ItemNumber} at {StoreId}", itemNumber, storeId);
                SelectedEntry = null;
            }
            else
            {
                StatusMessage = $"Error deleting entry: Entry not found";
                _logger?.LogError("Failed to delete allocation entry {ItemNumber}", itemNumber);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger?.LogError(ex, "Exception while deleting allocation entry");
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
            var fileName = $"AllocationBuddy_Export_{timestamp}.xlsx";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            var result = await _fileService.ExportToExcelAsync(Entries.ToList(), filePath);

            if (result.IsSuccess)
            {
                StatusMessage = $"Exported to {fileName}";
                _logger?.LogInformation("Exported {Count} entries to Excel", Entries.Count);
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
            var fileName = $"AllocationBuddy_Export_{timestamp}.csv";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = System.IO.Path.Combine(desktopPath, fileName);

            var result = await _fileService.ExportToCsvAsync(Entries.ToList(), filePath);

            if (result.IsSuccess)
            {
                StatusMessage = $"Exported to {fileName}";
                _logger?.LogInformation("Exported {Count} entries to CSV", Entries.Count);
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

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnRankFilterChanged(string value)
    {
        ApplyFilters();
    }

    /// <summary>
    /// Apply search and rank filters to entries
    /// </summary>
    private void ApplyFilters()
    {
        var filtered = Entries.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(e =>
                e.ItemNumber.ToLower().Contains(search) ||
                e.Description.ToLower().Contains(search) ||
                e.StoreId.ToLower().Contains(search) ||
                (e.StoreName?.ToLower().Contains(search) ?? false) ||
                (e.Category?.ToLower().Contains(search) ?? false));
        }

        // Apply rank filter
        if (RankFilter != "All")
        {
            if (Enum.TryParse<StoreRank>(RankFilter, out var rank))
            {
                filtered = filtered.Where(e => e.Rank == rank);
            }
        }

        FilteredEntries.Clear();
        foreach (var entry in filtered.OrderBy(e => e.StoreId).ThenBy(e => e.ItemNumber))
        {
            FilteredEntries.Add(entry);
        }
    }
}
