using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SAP.ViewModels;
using SAP.Core.Entities.ExpireWise;

namespace SAP.Views.ExpireWise;

public partial class ExpireWiseView : UserControl
{
    public ExpireWiseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpireWiseViewModel vm)
        {
            await vm.InitializeAsync();
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
