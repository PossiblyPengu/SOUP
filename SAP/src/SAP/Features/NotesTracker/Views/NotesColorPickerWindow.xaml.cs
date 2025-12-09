using System.Windows;
using System.Windows.Controls;

namespace SAP.Features.NotesTracker.Views
{
    public partial class NotesColorPickerWindow : Window
    {
        public string? SelectedHex { get; private set; }

        public NotesColorPickerWindow(string? initialHex = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialHex)) HexBox.Text = initialHex;
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string hex)
            {
                HexBox.Text = hex;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var txt = HexBox.Text?.Trim();
            if (!string.IsNullOrEmpty(txt) && txt[0] != '#') txt = "#" + txt;
            SelectedHex = txt;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
