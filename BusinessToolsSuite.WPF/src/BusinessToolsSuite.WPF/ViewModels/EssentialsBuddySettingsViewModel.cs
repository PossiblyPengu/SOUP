using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BusinessToolsSuite.Core.Entities.Settings;
using BusinessToolsSuite.Infrastructure.Services;

namespace BusinessToolsSuite.WPF.ViewModels;

public partial class EssentialsBuddySettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly string _appName = "EssentialsBuddy";

    [ObservableProperty]
    private int _lowStockThreshold = 10;

    [ObservableProperty]
    private int _outOfStockThreshold = 0;

    [ObservableProperty]
    private int _overstockThreshold = 100;

    [ObservableProperty]
    private string _defaultImportPath = string.Empty;

    [ObservableProperty]
    private string _defaultExportPath = string.Empty;

    [ObservableProperty]
    private int _autoRefreshIntervalMinutes = 0;

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private bool _showLowStockNotifications = true;

    [ObservableProperty]
    private bool _autoLoadLastSession = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public EssentialsBuddySettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync<EssentialsBuddySettings>(_appName);

            LowStockThreshold = settings.LowStockThreshold;
            OutOfStockThreshold = settings.OutOfStockThreshold;
            OverstockThreshold = settings.OverstockThreshold;
            DefaultImportPath = settings.DefaultImportPath;
            DefaultExportPath = settings.DefaultExportPath;
            AutoRefreshIntervalMinutes = settings.AutoRefreshIntervalMinutes;
            Theme = settings.Theme;
            ShowLowStockNotifications = settings.ShowLowStockNotifications;
            AutoLoadLastSession = settings.AutoLoadLastSession;

            StatusMessage = "Settings loaded successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new EssentialsBuddySettings
            {
                LowStockThreshold = LowStockThreshold,
                OutOfStockThreshold = OutOfStockThreshold,
                OverstockThreshold = OverstockThreshold,
                DefaultImportPath = DefaultImportPath,
                DefaultExportPath = DefaultExportPath,
                AutoRefreshIntervalMinutes = AutoRefreshIntervalMinutes,
                Theme = Theme,
                ShowLowStockNotifications = ShowLowStockNotifications,
                AutoLoadLastSession = AutoLoadLastSession
            };

            await _settingsService.SaveSettingsAsync(_appName, settings);
            StatusMessage = "Settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        _settingsService.ResetSettings(_appName);
        await LoadSettingsAsync();
        StatusMessage = "Settings reset to defaults";
    }
}
