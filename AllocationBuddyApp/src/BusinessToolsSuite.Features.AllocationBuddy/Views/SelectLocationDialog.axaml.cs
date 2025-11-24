using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BusinessToolsSuite.Shared.Controls;

namespace BusinessToolsSuite.Features.AllocationBuddy.Views;

public partial class SelectLocationDialog : UserControl
{
    public SelectLocationDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        InAppDialogHost.Instance?.CloseDialog(DataContext);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        InAppDialogHost.Instance?.CloseDialog(null);
    }
}
