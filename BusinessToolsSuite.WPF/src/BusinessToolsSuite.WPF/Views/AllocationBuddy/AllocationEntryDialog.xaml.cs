using System.Windows;
using System.Windows.Controls;
using BusinessToolsSuite.WPF.ViewModels;

namespace BusinessToolsSuite.WPF.Views.AllocationBuddy;

public partial class AllocationEntryDialog : UserControl
{
    public AllocationEntryDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with null result
        if (Tag is Action<BusinessToolsSuite.Core.Entities.AllocationBuddy.AllocationEntry?> closeAction)
        {
            closeAction(null);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AllocationEntryDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var entry = viewModel.ToEntity();
                if (Tag is Action<BusinessToolsSuite.Core.Entities.AllocationBuddy.AllocationEntry?> closeAction)
                {
                    closeAction(entry);
                }
            }
        }
    }
}
