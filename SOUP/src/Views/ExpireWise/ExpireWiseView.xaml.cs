using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Serilog;
using SOUP.Core.Entities.ExpireWise;
using SOUP.ViewModels;

namespace SOUP.Views.ExpireWise;

public partial class ExpireWiseView : UserControl
{
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

        vm.SelectedItems.Clear();
        // Sync selected items (SelectedItems contains object collection)
        if (ItemsGrid?.SelectedItems != null)
        {
            foreach (var obj in ItemsGrid.SelectedItems.Cast<object>())
            {
                if (obj is ExpirationItem item)
                {
                    vm.SelectedItems.Add(item);
                }
            }

            // Keep the single SelectedItem property pointing to first selected item
            vm.SelectedItem = ItemsGrid.SelectedItems.Cast<object>().OfType<ExpirationItem>().FirstOrDefault();
        }
    }
}
