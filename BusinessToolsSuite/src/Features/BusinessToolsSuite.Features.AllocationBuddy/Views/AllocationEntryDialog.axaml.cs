using Avalonia.Controls;
using Avalonia.Interactivity;
using BusinessToolsSuite.Features.AllocationBuddy.ViewModels;

namespace BusinessToolsSuite.Features.AllocationBuddy.Views;

public partial class AllocationEntryDialog : Window
{
    public AllocationEntryDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AllocationEntryDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var entry = viewModel.ToEntity();
                Close(entry);
            }
        }
    }
}
