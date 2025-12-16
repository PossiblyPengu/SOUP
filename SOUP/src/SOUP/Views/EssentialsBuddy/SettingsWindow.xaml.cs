using System.Windows;
using SOUP.ViewModels;

namespace SOUP.Views.EssentialsBuddy;

public partial class SettingsWindow : Window
{
    public SettingsWindow(EssentialsBuddySettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
