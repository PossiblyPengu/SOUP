using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Extensions.Logging;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Service responsible for coordinating display refresh operations for OrderLog items.
/// Handles grouping, filtering, sorting, and async background refresh for archived items.
/// </summary>
public class OrderDisplayRefreshCoordinator
{
    private readonly OrderGroupingService _groupingService;
    private readonly OrderSearchService _searchService;
    private readonly ILogger<OrderDisplayRefreshCoordinator>? _logger;
    private Task? _archivedRefreshTask;

    public OrderDisplayRefreshCoordinator(
        OrderGroupingService groupingService,
        OrderSearchService searchService,
        ILogger<OrderDisplayRefreshCoordinator>? logger = null)
    {
        _groupingService = groupingService;
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes the main display items collection with filtering, grouping, and sorting.
    /// </summary>
    /// <param name="source">Source collection (active items)</param>
    /// <param name="display">Display collection to update</param>
    /// <param name="sortByStatus">Whether to sort groups by status</param>
    /// <param name="sortStatusDescending">Sort direction for status</param>
    /// <param name="sortMode">Sort mode (Status, CreatedAt, VendorName)</param>
    /// <param name="searchQuery">Search query to filter</param>
    /// <param name="statusFilters">Status filters</param>
    /// <param name="filterStartDate">Start date filter</param>
    /// <param name="filterEndDate">End date filter</param>
    /// <param name="colorFilters">Color filters</param>
    /// <param name="noteTypeFilter">Note type filter</param>
    /// <param name="noteCategoryFilter">Note category filter</param>
    public void RefreshDisplayCollection(
        ObservableCollection<OrderItem> source,
        ObservableCollection<OrderItemGroup> display,
        bool sortByStatus,
        bool sortStatusDescending,
        OrderGroupingService.OrderLogSortMode sortMode,
        string? searchQuery,
        OrderItem.OrderStatus[]? statusFilters,
        DateTime? filterStartDate,
        DateTime? filterEndDate,
        string[]? colorFilters,
        NoteType? noteTypeFilter,
        NoteCategory? noteCategoryFilter)
    {
        // Apply search and filters first
        IEnumerable<OrderItem> filtered = source;

        if (_searchService.HasActiveFilters(searchQuery, statusFilters, filterStartDate, filterEndDate, colorFilters, noteTypeFilter, noteCategoryFilter))
        {
            filtered = _searchService.ApplyAllFilters(
                source,
                searchQuery,
                statusFilters,
                filterStartDate,
                filterEndDate,
                colorFilters,
                noteTypeFilter,
                noteCategoryFilter);
        }

        // Convert to ObservableCollection for grouping service
        var filteredCollection = new ObservableCollection<OrderItem>(filtered);

        // Use grouping service to build ordered display collection
        var built = _groupingService.BuildDisplayCollection(
            filteredCollection,
            sortByStatus,
            sortStatusDescending,
            sortMode);

        // Apply to display
        display.Clear();
        foreach (var group in built)
        {
            display.Add(group);
        }

        // Log grouping details for diagnostics
        try
        {
            var details = string.Join(',', built.Select(g => $"{(g.LinkedGroupId?.ToString() ?? "(null)")}:{g.Count}"));
            _logger?.LogInformation("OrderLog grouping built {GroupCount} groups: {Details}", built.Count, details);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to log grouping details");
        }
    }

    /// <summary>
    /// Refreshes the status-grouped collections (NotReady, OnDeck, InProgress).
    /// </summary>
    public void RefreshStatusGroups(
        ObservableCollection<OrderItem> source,
        ObservableCollection<OrderItemGroup> notReadyItems,
        ObservableCollection<OrderItemGroup> onDeckItems,
        ObservableCollection<OrderItemGroup> inProgressItems,
        string? searchQuery,
        OrderItem.OrderStatus[]? statusFilters,
        DateTime? filterStartDate,
        DateTime? filterEndDate,
        string[]? colorFilters,
        NoteType? noteTypeFilter,
        NoteCategory? noteCategoryFilter)
    {
        // Apply search and filters to items before grouping by status
        IEnumerable<OrderItem> filtered = source;

        if (_searchService.HasActiveFilters(searchQuery, statusFilters, filterStartDate, filterEndDate, colorFilters, noteTypeFilter, noteCategoryFilter))
        {
            filtered = _searchService.ApplyAllFilters(
                source,
                searchQuery,
                statusFilters,
                filterStartDate,
                filterEndDate,
                colorFilters,
                noteTypeFilter,
                noteCategoryFilter);
        }

        // Convert to ObservableCollection for grouping service
        var filteredCollection = new ObservableCollection<OrderItem>(filtered);

        // Delegate status-group population to the grouping service
        _groupingService.PopulateStatusGroups(filteredCollection, notReadyItems, onDeckItems, inProgressItems);
    }

    /// <summary>
    /// Refreshes the sticky notes collection with filtering.
    /// </summary>
    public void RefreshStickyNotes(
        ObservableCollection<OrderItem> source,
        ObservableCollection<OrderItem> stickyNotes,
        string? searchQuery,
        OrderItem.OrderStatus[]? statusFilters,
        DateTime? filterStartDate,
        DateTime? filterEndDate,
        string[]? colorFilters,
        NoteType? noteTypeFilter,
        NoteCategory? noteCategoryFilter)
    {
        stickyNotes.Clear();

        // Start with all sticky notes
        IEnumerable<OrderItem> notes = source.Where(i => i.IsStickyNote);

        // Apply search and filters if active
        if (_searchService.HasActiveFilters(searchQuery, statusFilters, filterStartDate, filterEndDate, colorFilters, noteTypeFilter, noteCategoryFilter))
        {
            notes = _searchService.ApplyAllFilters(
                notes,
                searchQuery,
                statusFilters,
                filterStartDate,
                filterEndDate,
                colorFilters,
                noteTypeFilter,
                noteCategoryFilter);
        }

        // Order by created date and add to collection
        foreach (var note in notes.OrderBy(i => i.CreatedAt))
        {
            stickyNotes.Add(note);
        }
    }

    /// <summary>
    /// Updates the LinkedItemCount property for all items based on their LinkedGroupId.
    /// </summary>
    public void UpdateLinkedItemCounts(IEnumerable<OrderItem> allItems)
    {
        // Group all items by their LinkedGroupId
        var linkedGroups = allItems
            .Where(i => i.LinkedGroupId != null)
            .GroupBy(i => i.LinkedGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Update each item's count (count - 1 to exclude itself)
        foreach (var item in allItems)
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
    /// Schedules a non-blocking async refresh of archived display items.
    /// If a refresh is already running, this returns immediately.
    /// </summary>
    public void RefreshArchivedDisplayItems(
        ObservableCollection<OrderItem> archivedSource,
        ObservableCollection<OrderItemGroup> displayArchived,
        string? searchQuery,
        OrderItem.OrderStatus[]? statusFilters,
        DateTime? filterStartDate,
        DateTime? filterEndDate,
        string[]? colorFilters,
        NoteType? noteTypeFilter,
        NoteCategory? noteCategoryFilter,
        Action updateDisplayCountsCallback)
    {
        if (_archivedRefreshTask != null && !_archivedRefreshTask.IsCompleted)
            return;

        _archivedRefreshTask = RefreshArchivedDisplayItemsAsync(
            archivedSource,
            displayArchived,
            searchQuery,
            statusFilters,
            filterStartDate,
            filterEndDate,
            colorFilters,
            noteTypeFilter,
            noteCategoryFilter,
            updateDisplayCountsCallback);
    }

    /// <summary>
    /// Asynchronously rebuilds archived display groups off the UI thread,
    /// then applies results back on UI thread to avoid freezes.
    /// </summary>
    private async Task RefreshArchivedDisplayItemsAsync(
        ObservableCollection<OrderItem> archivedSource,
        ObservableCollection<OrderItemGroup> displayArchived,
        string? searchQuery,
        OrderItem.OrderStatus[]? statusFilters,
        DateTime? filterStartDate,
        DateTime? filterEndDate,
        string[]? colorFilters,
        NoteType? noteTypeFilter,
        NoteCategory? noteCategoryFilter,
        Action updateDisplayCountsCallback)
    {
        // Snapshot source and apply filters on calling thread (fast)
        IEnumerable<OrderItem> filtered = archivedSource;

        if (_searchService.HasActiveFilters(searchQuery, statusFilters, filterStartDate, filterEndDate, colorFilters, noteTypeFilter, noteCategoryFilter))
        {
            filtered = _searchService.ApplyAllFilters(
                archivedSource,
                searchQuery,
                statusFilters,
                filterStartDate,
                filterEndDate,
                colorFilters,
                noteTypeFilter,
                noteCategoryFilter);
        }

        var snapshot = filtered.ToList();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Build groups on background thread
        var built = await Task.Run(() =>
        {
            var filteredCollection = new ObservableCollection<OrderItem>(snapshot);
            // For archived items, sort by CreatedAt (most recent first) for intuitive browsing
            return _groupingService.BuildDisplayCollection(
                filteredCollection,
                false,
                true,
                OrderGroupingService.OrderLogSortMode.CreatedAt);
        }).ConfigureAwait(false);

        sw.Stop();

        // Apply results back on UI thread
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                displayArchived.Clear();
                foreach (var group in built)
                {
                    displayArchived.Add(group);
                }
                updateDisplayCountsCallback();
            });

            _logger?.LogInformation(
                "RefreshArchivedDisplayItemsAsync built {Groups} groups from {Items} items in {Ms}ms",
                built.Count,
                snapshot.Count,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to apply archived display items");
        }
    }

    /// <summary>
    /// Calculates display counts from display collections.
    /// </summary>
    public (int DisplayItemsCount, int DisplayArchivedItemsCount, int TotalMembersCount) CalculateDisplayCounts(
        ObservableCollection<OrderItemGroup> displayItems,
        ObservableCollection<OrderItemGroup> displayArchivedItems,
        int activeItemsCount,
        int archivedItemsCount)
    {
        return (
            DisplayItemsCount: displayItems.Sum(g => g.Members.Count),
            DisplayArchivedItemsCount: displayArchivedItems.Sum(g => g.Members.Count),
            TotalMembersCount: activeItemsCount + archivedItemsCount
        );
    }
}
