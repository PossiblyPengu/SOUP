using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BusinessToolsSuite.Shared.Controls;

namespace BusinessToolsSuite.Features.AllocationBuddy.Views;

public partial class ConfirmDialog : UserControl
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetMessage(string message)
    {
        var tb = this.FindControl<Avalonia.Controls.TextBlock>("MessageText");
        if (tb != null) tb.Text = message;
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        InAppDialogHost.Instance?.CloseDialog(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        InAppDialogHost.Instance?.CloseDialog(false);
    }
}
