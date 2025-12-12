using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SAP.Core.Entities.ExpireWise;
using SAP.ViewModels;

namespace SAP.Views.ExpireWise;

public partial class ExpirationItemDialog : UserControl
{
    public ExpirationItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with null result
        if (Tag is Action<List<ExpirationItem>?> closeAction)
        {
            closeAction(null);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpirationItemDialogViewModel viewModel)
        {
            if (viewModel.IsBulkMode)
            {
                // Bulk mode - check if verified first
                if (!viewModel.BulkVerified)
                {
                    MessageBox.Show("Please verify SKUs before saving.", "Verification Required", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (viewModel.BulkFoundCount == 0)
                {
                    MessageBox.Show("No valid items to add. All SKUs were not found in the database.\n\nPlease add them via Import Dictionary first.", 
                        "No Valid Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show confirmation if some items were not found
                if (viewModel.BulkNotFoundCount > 0)
                {
                    var result = MessageBox.Show(
                        $"{viewModel.BulkNotFoundCount} item(s) were not found and will be skipped.\n\nDo you want to continue adding the {viewModel.BulkFoundCount} found item(s)?",
                        "Some Items Not Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                        return;
                }

                var items = viewModel.GetBulkItems();
                if (items.Count > 0)
                {
                    if (Tag is Action<List<ExpirationItem>?> closeAction)
                    {
                        closeAction(items);
                    }
                }
            }
            else
            {
                // Single item mode
                if (viewModel.IsValid())
                {
                    var item = viewModel.ToEntity();
                    if (Tag is Action<List<ExpirationItem>?> closeAction)
                    {
                        closeAction(new List<ExpirationItem> { item });
                    }
                }
            }
        }
    }

    private void SingleItemMode_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpirationItemDialogViewModel viewModel)
        {
            viewModel.SelectedTabIndex = 0;
        }
    }

    private void BulkInputMode_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpirationItemDialogViewModel viewModel)
        {
            viewModel.SelectedTabIndex = 1;
        }
    }
}
