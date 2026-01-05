using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Views;

public partial class OrderLogSettingsView : UserControl
{
    public OrderLogSettingsView()
    {
        InitializeComponent();
    }

    private void ClearArchived_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OrderLogViewModel vm) return;
        
        if (vm.ArchivedItems.Count == 0)
        {
            vm.StatusMessage = "No archived items to clear";
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to permanently delete all {vm.ArchivedItems.Count} archived items?\n\nThis action cannot be undone.",
            "Clear All Archived",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            ClearArchivedAsync(vm);
        }
    }

    private async void ClearArchivedAsync(OrderLogViewModel vm)
    {
        try
        {
            var count = vm.ArchivedItems.Count;
            await vm.ClearAllArchivedAsync();
            vm.StatusMessage = $"Cleared {count} archived items";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clear archived items");
        }
    }
}
