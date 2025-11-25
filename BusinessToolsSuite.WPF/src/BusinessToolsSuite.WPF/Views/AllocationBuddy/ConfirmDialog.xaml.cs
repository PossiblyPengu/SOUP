using System;
using System.Windows;
using System.Windows.Controls;

namespace BusinessToolsSuite.WPF.Views.AllocationBuddy;

public partial class ConfirmDialog : UserControl
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public void SetMessage(string message)
    {
        MessageText.Text = message;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with true result
        if (Tag is Action<bool?> closeAction)
        {
            closeAction(true);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Close dialog with false result
        if (Tag is Action<bool?> closeAction)
        {
            closeAction(false);
        }
    }
}
