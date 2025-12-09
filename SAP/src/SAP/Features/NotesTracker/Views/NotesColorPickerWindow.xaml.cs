using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SAP.Features.NotesTracker.Views;

public partial class NotesColorPickerWindow : Window
{
    private static readonly Regex HexColorRegex = new(@"^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$", RegexOptions.Compiled);

    public string? SelectedHex { get; private set; }

    public NotesColorPickerWindow(string? initialHex = null)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(initialHex))
        {
            HexBox.Text = initialHex;
            UpdatePreview(initialHex);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            HexBox.Text = hex;
        }
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = HexBox.Text?.Trim();
        if (IsValidHexColor(text))
        {
            UpdatePreview(NormalizeHexColor(text!));
        }
    }

    private void UpdatePreview(string hexColor)
    {
        try
        {
            ColorPreviewBorder.Background = new BrushConverter().ConvertFromString(hexColor) as Brush;
        }
        catch
        {
            // Invalid color format
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = HexBox.Text?.Trim();

        if (!IsValidHexColor(text))
        {
            MessageBox.Show(this, "Please enter a valid hex color (e.g., #FF5733 or FF5733).",
                "Invalid Color", MessageBoxButton.OK, MessageBoxImage.Warning);
            HexBox.Focus();
            return;
        }

        SelectedHex = NormalizeHexColor(text!);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static bool IsValidHexColor(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) && HexColorRegex.IsMatch(text);
    }

    private static string NormalizeHexColor(string text)
    {
        text = text.TrimStart('#');

        // Expand shorthand (e.g., "F00" -> "FF0000")
        if (text.Length == 3)
        {
            text = $"{text[0]}{text[0]}{text[1]}{text[1]}{text[2]}{text[2]}";
        }

        return $"#{text.ToUpperInvariant()}";
    }
}
