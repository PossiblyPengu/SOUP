using System.Windows;
using System.Windows.Controls;

namespace SOUP.Views.AllocationBuddy;

public partial class PasteDataDialog : UserControl
{
    public string? PastedText { get; private set; }

    public PasteDataDialog()
    {
        InitializeComponent();
        
        PasteTextBox.TextChanged += PasteTextBox_TextChanged;
        PasteTextBox.Focus();
    }

    private void PasteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = PasteTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            PreviewText.Text = "Paste data to preview";
            ImportButton.IsEnabled = false;
            return;
        }

        // Count lines and estimate entries
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var lineCount = lines.Length;
        
        // Try to detect if first line is header
        var hasHeader = lineCount > 0 && 
            (lines[0].Contains("Store", StringComparison.OrdinalIgnoreCase) ||
             lines[0].Contains("Location", StringComparison.OrdinalIgnoreCase) ||
             lines[0].Contains("Item", StringComparison.OrdinalIgnoreCase));

        var dataLines = hasHeader ? lineCount - 1 : lineCount;
        
        PreviewText.Text = $"📊 {dataLines} data row{(dataLines == 1 ? "" : "s")} detected{(hasHeader ? " (header found)" : "")}";
        ImportButton.IsEnabled = dataLines > 0;
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PasteTextBox.Text))
        {
            PreviewText.Text = "⚠️ Please paste some data first";
            return;
        }

        PastedText = PasteTextBox.Text;

        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.DialogResult = true;
            window.Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        PastedText = null;
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
