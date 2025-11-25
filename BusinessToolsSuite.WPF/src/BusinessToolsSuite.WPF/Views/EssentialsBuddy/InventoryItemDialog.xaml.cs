using System.Windows;
using System.Windows.Controls;
using BusinessToolsSuite.WPF.ViewModels;

namespace BusinessToolsSuite.WPF.Views.EssentialsBuddy;

public partial class InventoryItemDialog : UserControl
{
    public InventoryItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with null result
        if (Tag is Action<BusinessToolsSuite.Core.Entities.EssentialsBuddy.InventoryItem?> closeAction)
        {
            closeAction(null);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryItemDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var item = viewModel.ToEntity();
                if (Tag is Action<BusinessToolsSuite.Core.Entities.EssentialsBuddy.InventoryItem?> closeAction)
                {
                    closeAction(item);
                }
            }
        }
    }
}
