using Avalonia.Controls;
using Avalonia.Interactivity;
using BusinessToolsSuite.Features.EssentialsBuddy.ViewModels;

namespace BusinessToolsSuite.Features.EssentialsBuddy.Views;

public partial class InventoryItemDialog : Window
{
    public InventoryItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryItemDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var item = viewModel.ToEntity();
                Close(item);
            }
        }
    }
}
