using Avalonia.Controls;
using Avalonia.Interactivity;
using BusinessToolsSuite.Features.AllocationBuddy.ViewModels;
using BusinessToolsSuite.Shared.Controls;

namespace BusinessToolsSuite.Features.AllocationBuddy.Views;

public partial class AllocationEntryDialog : UserControl
{
    public AllocationEntryDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        InAppDialogHost.Instance?.CloseDialog(null);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AllocationEntryDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var entry = viewModel.ToEntity();
                InAppDialogHost.Instance?.CloseDialog(entry);
            }
        }
    }
}
