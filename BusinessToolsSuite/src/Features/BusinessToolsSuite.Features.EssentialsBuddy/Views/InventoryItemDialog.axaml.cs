using Avalonia.Controls;
using Avalonia.Interactivity;
using BusinessToolsSuite.Features.EssentialsBuddy.ViewModels;
using BusinessToolsSuite.Shared.Controls;

namespace BusinessToolsSuite.Features.EssentialsBuddy.Views;

public partial class InventoryItemDialog : UserControl
{
    public InventoryItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        InAppDialogHost.Instance?.CloseDialog(null);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryItemDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var item = viewModel.ToEntity();
                InAppDialogHost.Instance?.CloseDialog(item);
            }
        }
    }
}
