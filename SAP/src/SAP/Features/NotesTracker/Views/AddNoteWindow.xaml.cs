using System.Windows;
using SAP.Features.NotesTracker.Models;

namespace SAP.Features.NotesTracker.Views
{
    public partial class AddNoteWindow : Window
    {
        public NoteItem? Result { get; private set; }

        public AddNoteWindow()
        {
            InitializeComponent();
        }

        private async void PickColorBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new NotesColorPickerWindow("#B56576");
            picker.Owner = this;
            if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedHex))
            {
                ColorPreview.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(picker.SelectedHex);
                ColorPreview.Tag = picker.SelectedHex;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var note = new NoteItem
            {
                VendorName = VendorBox.Text,
                TransferNumbers = TransfersBox.Text,
                WhsShipmentNumbers = WhsBox.Text,
                ColorHex = (ColorPreview.Tag as string) ?? "#B56576",
                CreatedAt = System.DateTime.UtcNow
            };
            Result = note;
            DialogResult = true;
            Close();
        }
    }
}
