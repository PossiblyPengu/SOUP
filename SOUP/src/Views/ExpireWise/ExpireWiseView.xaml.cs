using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Serilog;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Features.ExpireWise.Helpers;
using SOUP.ViewModels;

namespace SOUP.Views.ExpireWise;

public partial class ExpireWiseView : UserControl
{
    private KeyboardShortcutManager? _shortcutManager;
    private bool _suppressSelectionChanged;

    public ExpireWiseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpireWiseViewModel vm)
        {
            vm.FocusSearchRequested += OnFocusSearchRequested;
            vm.GroupByStoreChanged += OnGroupByStoreChanged;
            InitializeViewModelAsync(vm);

            // Apply initial grouping state
            UpdateGrouping(vm.GroupByStore);

            // Register keyboard shortcuts
            _shortcutManager = new KeyboardShortcutManager();
            _shortcutManager.RegisterShortcuts(this, vm, OnFocusSearchRequested);
        }
    }

    private async void InitializeViewModelAsync(ExpireWiseViewModel vm)
    {
        try
        {
            await vm.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize ExpireWise");
        }
    }

    private void OnFocusSearchRequested()
    {
        SearchBox?.Focus();
        SearchBox?.SelectAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe events to prevent memory leaks
        if (DataContext is ExpireWiseViewModel vm)
        {
            vm.FocusSearchRequested -= OnFocusSearchRequested;
            vm.GroupByStoreChanged -= OnGroupByStoreChanged;
        }
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnGroupByStoreChanged(bool groupByStore)
    {
        UpdateGrouping(groupByStore);
    }

    private void UpdateGrouping(bool groupByStore)
    {
        if (Resources["GroupedItemsView"] is CollectionViewSource cvs)
        {
            cvs.GroupDescriptions.Clear();
            if (groupByStore)
            {
                cvs.GroupDescriptions.Add(new PropertyGroupDescription("Location"));
            }
            cvs.View?.Refresh();
        }
    }

    private void OnItemsGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ExpireWiseViewModel vm) return;
        if (_suppressSelectionChanged) return;

        vm.SelectedItems.Clear();
        if (ItemsGrid?.SelectedItems == null) return;

        var selectedItems = ItemsGrid.SelectedItems.Cast<object>().OfType<ExpirationItem>().ToList();
        if (selectedItems.Count <= 1)
        {
            // simple sync
            foreach (var it in selectedItems) vm.SelectedItems.Add(it);
            vm.SelectedItem = selectedItems.FirstOrDefault();
            return;
        }

        // Enforce that multi-selection is limited to the same store and the currently viewed month
        var first = selectedItems.First();
        var allowedLocation = first.Location ?? string.Empty;
        var allowedYear = vm.CurrentMonth.Year;
        var allowedMonth = vm.CurrentMonth.Month;

        var allowed = selectedItems.Where(i => (i.Location ?? string.Empty) == allowedLocation
                                              && i.ExpiryDate.Year == allowedYear
                                              && i.ExpiryDate.Month == allowedMonth).ToList();

        if (allowed.Count != selectedItems.Count)
        {
            // Remove disallowed selections from UI and reapply allowed selection
            _suppressSelectionChanged = true;
            ItemsGrid.SelectedItems.Clear();
            foreach (var it in allowed) ItemsGrid.SelectedItems.Add(it);
            _suppressSelectionChanged = false;
        }

        foreach (var it in allowed) vm.SelectedItems.Add(it);
        vm.SelectedItem = allowed.FirstOrDefault();
    }
}
