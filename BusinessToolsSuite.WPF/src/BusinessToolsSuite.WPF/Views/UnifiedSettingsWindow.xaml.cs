using System.Windows;
using BusinessToolsSuite.WPF.ViewModels;

namespace BusinessToolsSuite.WPF.Views;

public partial class UnifiedSettingsWindow : Window
{
    public UnifiedSettingsWindow(UnifiedSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
