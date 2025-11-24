using Avalonia.Controls;
using Avalonia.Interactivity;
using BusinessToolsSuite.Features.ExpireWise.ViewModels;
using BusinessToolsSuite.Shared.Controls;

namespace BusinessToolsSuite.Features.ExpireWise.Views;

public partial class ExpirationItemDialog : UserControl
{
    public ExpirationItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        InAppDialogHost.Instance?.CloseDialog(null);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExpirationItemDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var item = viewModel.ToEntity();
                InAppDialogHost.Instance?.CloseDialog(item);
            }
        }
    }
}
