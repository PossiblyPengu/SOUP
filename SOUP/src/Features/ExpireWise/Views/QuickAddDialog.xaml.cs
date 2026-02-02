using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SOUP.Features.ExpireWise.Views;

/// <summary>
/// Quick Add modal dialog for fast single-item entry in ExpireWise
/// </summary>
public partial class QuickAddDialog : UserControl
{
    public QuickAddDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Auto-focus the SKU input when dialog loads
        Dispatcher.BeginInvoke(new Action(() =>
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
    /// Cancel button handler - closes dialog without saving
    /// </summary>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (Tag is Action<object?> closeAction)
        {
            closeAction(null);
        }
    }

    /// <summary>
    /// Close button handler (X button) - same as cancel
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Cancel_Click(sender, e);
    }

    /// <summary>
    /// Focus the SKU input programmatically (called from parent view)
    /// </summary>
    public void FocusSkuInput()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SkuInput.Focus();
            SkuInput.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Clear the SKU input and refocus (called after adding to queue)
    /// </summary>
    public void ClearAndRefocus()
    {
        SkuInput.Clear();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SkuInput.Focus();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
