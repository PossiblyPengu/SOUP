using System.Windows;
using System.Windows.Controls;
using SAP.ViewModels;
using SAP.Windows;

namespace SAP.Views;

public partial class UnifiedSettingsWindow : Window
{
    private readonly UnifiedSettingsViewModel _viewModel;

    public UnifiedSettingsWindow(UnifiedSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        
        // Fire-and-forget initialization to avoid blocking window opening
        Loaded += (s, e) => _ = InitializeViewModelAsync();
    }

    private async System.Threading.Tasks.Task InitializeViewModelAsync()
    {
        try
        {
            await _viewModel.InitializeAsync().ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Error(ex, "Error initializing settings");
        }
    }

    /// <summary>
    /// Opens the About dialog
    /// </summary>
    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    /// <summary>
    /// Handle tab selection to load dictionary lazily when Dictionary Management tab is selected
    /// </summary>
    internal async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is TabControl tabControl && 
            tabControl.SelectedItem is TabItem selectedTab)
        {
            var tabHeader = selectedTab.Header?.ToString() ?? "(null)";
            Serilog.Log.Information("Tab selected: {TabHeader}", tabHeader);
            
            if (tabHeader.Contains("Dictionary"))
            {
                Serilog.Log.Information("Dictionary Management tab selected. IsInitialized={IsInit}, IsLoading={IsLoading}", 
                    _viewModel.DictionaryManagement.IsInitialized, 
                    _viewModel.DictionaryManagement.IsLoading);
                    
                // Load dictionary only when the tab is selected and not already initialized
                if (!_viewModel.DictionaryManagement.IsInitialized && !_viewModel.DictionaryManagement.IsLoading)
                {
                    Serilog.Log.Information("Calling LoadDictionaryAsync...");
                    await _viewModel.DictionaryManagement.LoadDictionaryAsync();
                    Serilog.Log.Information("LoadDictionaryAsync completed. FilteredItems.Count={Count}", 
                        _viewModel.DictionaryManagement.FilteredItems?.Count ?? -1);
                }
            }
        }
    }
}
