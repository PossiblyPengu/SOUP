using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Serilog;
using SOUP.ViewModels;

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
                view.SortDescriptions.Add(new SortDescription("StatusSortOrder", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("ItemNumber", ListSortDirection.Ascending));
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to apply default sort and grouping for EssentialsBuddy");
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
    }
}
