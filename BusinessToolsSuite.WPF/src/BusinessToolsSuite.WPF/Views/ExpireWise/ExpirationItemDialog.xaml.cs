using System;
using System.Windows;
using System.Windows.Controls;
using BusinessToolsSuite.WPF.ViewModels;

namespace BusinessToolsSuite.WPF.Views.ExpireWise;

public partial class ExpirationItemDialog : UserControl
{
    public ExpirationItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with null result
        if (Tag is Action<BusinessToolsSuite.Core.Entities.ExpireWise.ExpirationItem?> closeAction)
        {
            closeAction(null);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpirationItemDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var item = viewModel.ToEntity();
                if (Tag is Action<BusinessToolsSuite.Core.Entities.ExpireWise.ExpirationItem?> closeAction)
                {
                    closeAction(item);
                }
            }
        }
    }
}
