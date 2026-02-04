using System;
using System.ComponentModel;
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
            vm.PropertyChanged += OnViewModelPropertyChanged;
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
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExpireWiseViewModel.SelectedStore))
        {
            // Scroll main content to top when store changes
            MainContentScrollViewer?.ScrollToTop();
        }
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
        // No longer using DataGrid - timeline view handles selection differently
        // This method is kept for backward compatibility if DataGrid is re-enabled
    }

    private void TimelineScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        // Enable horizontal scrolling with mouse wheel on the timeline
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
