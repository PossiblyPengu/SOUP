using System.Windows;
using System.Windows.Input;
using SAP.Features.NotesTracker.ViewModels;
using SAP.Features.NotesTracker.Views;

namespace SAP.Windows
{
    public partial class NotesWidgetWindow : Window
    {
        private readonly NotesTrackerViewModel _viewModel;

        public NotesWidgetWindow(NotesTrackerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            NotesView.DataContext = _viewModel;
            
            // Position on right side of screen
            Loaded += NotesWidgetWindow_Loaded;
        }

        private async void NotesWidgetWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
            
            // Position window on right side of screen
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 10;
            Top = workArea.Top + 10;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddNote_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddNoteWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (addWindow.ShowDialog() == true && addWindow.Result != null)
            {
                _ = _viewModel.AddNoteAsync(addWindow.Result);
            }
        }
    }
}
