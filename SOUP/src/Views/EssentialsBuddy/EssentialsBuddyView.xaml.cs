using System;
using System.Windows;
using System.Windows.Controls;
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
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize EssentialsBuddy");
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
