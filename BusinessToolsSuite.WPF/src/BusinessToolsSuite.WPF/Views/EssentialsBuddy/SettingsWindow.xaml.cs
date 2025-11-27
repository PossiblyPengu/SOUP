using System.Windows;
using BusinessToolsSuite.WPF.ViewModels;

namespace BusinessToolsSuite.WPF.Views.EssentialsBuddy;

public partial class SettingsWindow : Window
{
    public SettingsWindow(EssentialsBuddySettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
