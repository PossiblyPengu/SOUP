using System;
using System.Windows;
using System.Windows.Controls;
using SAP.ViewModels;

namespace SAP.Views.EssentialsBuddy;

public partial class EssentialsBuddyView : UserControl
{
    public EssentialsBuddyView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is EssentialsBuddyViewModel vm)
            {
                vm.FocusSearchRequested += OnFocusSearchRequested;
                await vm.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize EssentialsBuddy: {ex.Message}");
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
