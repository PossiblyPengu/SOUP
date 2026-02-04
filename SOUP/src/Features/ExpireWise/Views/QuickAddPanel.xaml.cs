using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SOUP.Features.ExpireWise.Views;

/// <summary>
/// Quick Add panel for fast single-item entry in ExpireWise
/// </summary>
public partial class QuickAddPanel : UserControl
{
    public QuickAddPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Auto-focus the SKU input when panel loads
        _ = Dispatcher.BeginInvoke(new System.Action(() =>
        {
            SkuInput.Focus();
            SkuInput.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Restrict quantity input to numeric values only
    /// </summary>
    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only digits
        e.Handled = !IsTextNumeric(e.Text);
    }

    private static bool IsTextNumeric(string text)
    {
        return Regex.IsMatch(text, @"^[0-9]+$");
    }

    /// <summary>
    /// Focus the SKU input programmatically (called from parent view)
    /// </summary>
    public void FocusSkuInput()
    {
        // Focus and select all text in SKU input
        _ = Dispatcher.BeginInvoke(new System.Action(() =>
        {
            SkuInput.Focus();
            SkuInput.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Clear the SKU input and refocus (called after "Add and Continue")
    /// </summary>
    public void ClearAndRefocus()
    {
        SkuInput.Clear();
        _ = Dispatcher.BeginInvoke(new System.Action(() =>
        {
            SkuInput.Focus();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
