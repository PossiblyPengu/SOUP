using System.Windows;
using SAP.Features.NotesTracker.ViewModels;

namespace SAP.Windows
{
    public partial class NotesTrackerWindow : Window
    {
        private readonly NotesTrackerViewModel _viewModel;

        public NotesTrackerWindow(NotesTrackerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            NotesView.DataContext = _viewModel;
            Loaded += NotesTrackerWindow_Loaded;
        }

        private async void NotesTrackerWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }
    }
}
