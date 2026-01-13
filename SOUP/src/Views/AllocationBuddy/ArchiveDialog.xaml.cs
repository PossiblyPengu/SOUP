using System.Windows;
using System.Windows.Controls;
using SOUP.ViewModels;

namespace SOUP.Views.AllocationBuddy;

public partial class ArchiveDialog : UserControl
{
    public AllocationBuddyRPGViewModel.ArchiveDialogResult? Result { get; private set; }

    public ArchiveDialog()
    {
        InitializeComponent();

        // Set default name with timestamp
        NameTextBox.Text = $"Allocation {DateTime.Now:MMM d, yyyy}";
        NameTextBox.SelectAll();
        NameTextBox.Focus();
    }

    private void ArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Please enter an archive name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        Result = new()
        {
            Name = NameTextBox.Text.Trim(),
            Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim()
        };

        // Close the dialog by finding the parent window
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.DialogResult = true;
            window.Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
