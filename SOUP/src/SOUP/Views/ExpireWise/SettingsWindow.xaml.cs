using System.Windows;
using SOUP.ViewModels;

namespace SOUP.Views.ExpireWise;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ExpireWiseSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
