using System;
using System.Windows;
using System.Windows.Controls;
using SOUP.ViewModels;

namespace SOUP.Views.AllocationBuddy;

public partial class SelectLocationDialog : UserControl
{
    public SelectLocationDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with the view model
        if (Tag is Action<SelectLocationDialogViewModel?> closeAction)
        {
            closeAction(DataContext as SelectLocationDialogViewModel);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with null result
        if (Tag is Action<SelectLocationDialogViewModel?> closeAction)
        {
            closeAction(null);
        }
    }
}
