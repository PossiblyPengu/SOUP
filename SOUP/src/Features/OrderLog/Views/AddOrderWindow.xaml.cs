using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Views;

public partial class AddOrderWindow : Window
{
    private const string DefaultColorHex = "#B56576";

    public OrderItem? Result { get; private set; }

    private string _selectedColorHex = DefaultColorHex;

    public AddOrderWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        VendorBox.Focus();
        VendorBox.SelectAll();
        UpdateOkButtonState();
    }

    private void PickColorBtn_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OrderColorPickerWindow(_selectedColorHex) { Owner = this };

        if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedColor))
        {
            _selectedColorHex = picker.SelectedColor;
            try
            {
                ColorPreview.Background = new BrushConverter().ConvertFromString(_selectedColorHex) as Brush;
            }
            catch
            {
                // Invalid color, keep current
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var vendorName = VendorBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(vendorName))
        {
            MessageBox.Show(this, "Please enter a vendor name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            VendorBox.Focus();
            return;
        }

        Result = new OrderItem
        {
            VendorName = vendorName,
            TransferNumbers = TransfersBox.Text?.Trim() ?? string.Empty,
            WhsShipmentNumbers = WhsBox.Text?.Trim() ?? string.Empty,
            ColorHex = _selectedColorHex,
            CreatedAt = DateTime.UtcNow
        };

        DialogResult = true;
        Close();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateOkButtonState();
    }

    private void UpdateOkButtonState()
    {
        if (OkButton != null)
        {
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(VendorBox?.Text);
        }
    }
}
