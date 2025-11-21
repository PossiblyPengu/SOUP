using Avalonia.Controls;
using Avalonia.Interactivity;
using BusinessToolsSuite.Features.ExpireWise.ViewModels;

namespace BusinessToolsSuite.Features.ExpireWise.Views;

public partial class ExpirationItemDialog : Window
{
    public ExpirationItemDialog()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExpirationItemDialogViewModel viewModel)
        {
            if (viewModel.IsValid())
            {
                var item = viewModel.ToEntity();
                Close(item);
            }
        }
    }
}
