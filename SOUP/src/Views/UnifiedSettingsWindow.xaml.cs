using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SOUP.ViewModels;
using SOUP.Windows;
using SOUP.Features.OrderLog.Views;
using SOUP.Services;

namespace SOUP.Views;

public partial class UnifiedSettingsWindow : Window
{
    private readonly UnifiedSettingsViewModel _viewModel;

    public UnifiedSettingsWindow(UnifiedSettingsViewModel viewModel, string? initialTab = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        
        // Hide tabs for disabled modules
        var moduleConfig = ModuleConfiguration.Instance;
        TabAllocation.Visibility = moduleConfig.AllocationBuddyEnabled ? Visibility.Visible : Visibility.Collapsed;
        TabEssentials.Visibility = moduleConfig.EssentialsBuddyEnabled ? Visibility.Visible : Visibility.Collapsed;
        TabExpireWise.Visibility = moduleConfig.ExpireWiseEnabled ? Visibility.Visible : Visibility.Collapsed;
        TabOrderLog.Visibility = moduleConfig.OrderLogEnabled ? Visibility.Visible : Visibility.Collapsed;
        
        // Initialize asynchronously on window load
        Loaded += OnWindowLoaded;
        
        // Select initial tab if specified, or first visible module tab
        if (!string.IsNullOrEmpty(initialTab))
        {
            SelectTab(initialTab);
        }
        else
        {
            // Select first visible tab
            SelectFirstVisibleTab();
        }
    }
    
    /// <summary>
    /// Selects the first visible tab.
    /// </summary>
    private void SelectFirstVisibleTab()
    {
        // Application tab is always visible
        TabApplication.IsChecked = true;
    }
    
    /// <summary>
    /// Selects a tab by name.
    /// </summary>
    public void SelectTab(string tabName)
    {
        var moduleConfig = ModuleConfiguration.Instance;
        switch (tabName.ToLowerInvariant())
        {
            case "application":
                TabApplication.IsChecked = true;
                break;
            case "allocation" when moduleConfig.AllocationBuddyEnabled: 
                TabAllocation.IsChecked = true; 
                break;
            case "essentials" when moduleConfig.EssentialsBuddyEnabled: 
                TabEssentials.IsChecked = true; 
                break;
            case "expirewise" when moduleConfig.ExpireWiseEnabled: 
                TabExpireWise.IsChecked = true; 
                break;
            case "dictionary": 
                TabDictionary.IsChecked = true; 
                break;
            case "orderlog" when moduleConfig.OrderLogEnabled: 
                TabOrderLog.IsChecked = true; 
                break;
            case "externaldata": 
                TabExternalData.IsChecked = true; 
                break;
            default:
                SelectFirstVisibleTab();
                break;
        }
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
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
    /// Handles title bar dragging for borderless window
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click doesn't maximize for dialog windows
            return;
        }
        DragMove();
    }

    /// <summary>
    /// Closes the window
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
    /// Handle tab navigation with the new RadioButton-based tabs
    /// </summary>
    private async void OnTabChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            // Hide all panels
            PanelApplication.Visibility = Visibility.Collapsed;
            PanelAllocation.Visibility = Visibility.Collapsed;
            PanelEssentials.Visibility = Visibility.Collapsed;
            PanelExpireWise.Visibility = Visibility.Collapsed;
            PanelDictionary.Visibility = Visibility.Collapsed;
            PanelOrderLog.Visibility = Visibility.Collapsed;
            PanelExternalData.Visibility = Visibility.Collapsed;

            // Show the selected panel
            if (TabApplication.IsChecked == true)
            {
                PanelApplication.Visibility = Visibility.Visible;
            }
            else if (TabAllocation.IsChecked == true)
            {
                PanelAllocation.Visibility = Visibility.Visible;
            }
            else if (TabEssentials.IsChecked == true)
            {
                PanelEssentials.Visibility = Visibility.Visible;
            }
            else if (TabExpireWise.IsChecked == true)
            {
                PanelExpireWise.Visibility = Visibility.Visible;
            }
            else if (TabDictionary.IsChecked == true)
            {
                PanelDictionary.Visibility = Visibility.Visible;
                
                // Load dictionary lazily
                if (!_viewModel.DictionaryManagement.IsInitialized && !_viewModel.DictionaryManagement.IsLoading)
                {
                    Serilog.Log.Information("Dictionary tab selected, loading dictionary...");
                    await _viewModel.DictionaryManagement.LoadDictionaryAsync();
                }
            }
            else if (TabOrderLog.IsChecked == true)
            {
                PanelOrderLog.Visibility = Visibility.Visible;

                // Lazy-create the OrderLog settings view to avoid loading heavy controls unnecessarily
                try
                {
                    if (PanelOrderLog.Child == null)
                    {
                        var orderLogView = new OrderLogSettingsView();
                        orderLogView.DataContext = _viewModel.OrderLogSettings;
                        PanelOrderLog.Child = orderLogView;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to create OrderLogSettingsView lazily");
                }
            }
            else if (TabExternalData.IsChecked == true)
            {
                PanelExternalData.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to handle tab change");
        }
    }

    /// <summary>
    /// Handle tab selection to load dictionary lazily when Dictionary Management tab is selected
    /// </summary>
    internal async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to handle tab selection");
        }
    }
}
