using System.Windows;
using SOUP.ViewModels;
using SOUP.Views;

namespace SOUP.Views.AllocationBuddy;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OpenUnifiedSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var unifiedVm = App.GetService<UnifiedSettingsViewModel>();
            var unifiedWindow = new UnifiedSettingsWindow(unifiedVm);
            unifiedWindow.Owner = this.Owner;
            unifiedWindow.Show();
            this.Close();
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to open UnifiedSettingsWindow from AllocationBuddy Settings stub");
            MessageBox.Show("Unable to open Unified Settings window. See logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
