using System.Windows;
using SAP.ViewModels;

namespace SAP.Views.ExpireWise;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ExpireWiseSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) => await viewModel.InitializeAsync();
    }
}
