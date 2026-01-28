using System;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Serilog;
using SOUP.ViewModels;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using SOUP.Core;

namespace SOUP.Views.EssentialsBuddy;

public partial class EssentialsBuddyView : UserControl
{
    public EssentialsBuddyView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EssentialsBuddyViewModel vm)
        {
            vm.FocusSearchRequested += OnFocusSearchRequested;
            InitializeViewModelAsync(vm);
        }
    }

    private async void InitializeViewModelAsync(EssentialsBuddyViewModel vm)
    {
        try
        {
            await vm.InitializeAsync();

            // Ensure the grid shows status-sorted order and displays the sort glyph.
            ApplyDefaultSort();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize EssentialsBuddy");
        }
    }

    private void ApplyDefaultSort()
    {
        try
        {
            if (ItemsGrid == null) return;

            var view = CollectionViewSource.GetDefaultView(ItemsGrid.ItemsSource);
            if (view != null)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("StatusSortOrder", System.ComponentModel.ListSortDirection.Ascending));
                view.SortDescriptions.Add(new System.ComponentModel.SortDescription("ItemNumber", System.ComponentModel.ListSortDirection.Ascending));
            }

            // Set the header glyph on the Status column
            var statusColumn = ItemsGrid.Columns?.FirstOrDefault(c => string.Equals(c.SortMemberPath, "StatusSortOrder", StringComparison.OrdinalIgnoreCase));
            if (statusColumn != null)
            {
                statusColumn.SortDirection = System.ComponentModel.ListSortDirection.Ascending;
            }

            // Load any persisted column widths
            LoadColumnWidths();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to apply default sort for EssentialsBuddy grid");
        }
    }

    private void OnFocusSearchRequested()
    {
        SearchBox?.Focus();
        SearchBox?.SelectAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EssentialsBuddyViewModel vm)
        {
            vm.FocusSearchRequested -= OnFocusSearchRequested;
        }
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        // Persist any adjusted column widths
        try
        {
            SaveColumnWidths();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to save column widths on unload");
        }
    }

    private string GetColumnsStatePath()
    {
        try
        {
            var dir = AppPaths.EssentialsBuddyDir;
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "columns.json");
        }
        catch
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SOUP", "EssentialsBuddy", "columns.json");
        }
    }

    private void SaveColumnWidths()
    {
        if (ItemsGrid == null || ItemsGrid.Columns == null) return;

        var map = new Dictionary<string, double>();
        foreach (var col in ItemsGrid.Columns)
        {
            var key = !string.IsNullOrEmpty(col.SortMemberPath) ? col.SortMemberPath : col.Header?.ToString() ?? string.Empty;
            // Save pixel width if available, otherwise use DisplayIndex measure
            var width = col.ActualWidth;
            map[key] = width;
        }

        var path = GetColumnsStatePath();
        var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void LoadColumnWidths()
    {
        try
        {
            if (ItemsGrid == null || ItemsGrid.Columns == null) return;
            var path = GetColumnsStatePath();
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var map = JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? new Dictionary<string, double>();

            foreach (var col in ItemsGrid.Columns)
            {
                var key = !string.IsNullOrEmpty(col.SortMemberPath) ? col.SortMemberPath : col.Header?.ToString() ?? string.Empty;
                if (map.TryGetValue(key, out var w) && w > 0)
                {
                    col.Width = new DataGridLength(w);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to load saved column widths");
        }
    }
}
