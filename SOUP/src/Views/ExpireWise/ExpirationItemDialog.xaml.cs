using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SOUP.Core.Entities.ExpireWise;
using SOUP.ViewModels;

namespace SOUP.Views.ExpireWise;

public partial class ExpirationItemDialog : UserControl
{
    public ExpirationItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (Tag is Action<List<ExpirationItem>?> closeAction)
        {
            closeAction(null);
        }
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExpirationItemDialogViewModel viewModel)
        {
            if (!viewModel.IsVerified)
            {
                MessageBox.Show("Please verify SKUs before adding.", "Verification Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (viewModel.ParsedItems.Count == 0)
            {
                MessageBox.Show("No items to add.", "No Items",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Warn if some items not found
            if (viewModel.NotFoundCount > 0)
            {
                var result = MessageBox.Show(
                    $"{viewModel.NotFoundCount} item(s) not in database will be added with their SKU as the item number.\n\nContinue?",
                    "Some Items Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            var items = viewModel.BuildItems();
            if (items.Count > 0 && Tag is Action<List<ExpirationItem>?> closeAction)
            {
                closeAction(items);
            }
        }
    }
}
