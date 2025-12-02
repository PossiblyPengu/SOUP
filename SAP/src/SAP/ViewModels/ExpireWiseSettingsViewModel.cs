using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SAP.Core.Entities.Settings;
using SAP.Infrastructure.Services;

namespace SAP.ViewModels;

public partial class ExpireWiseSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly string _appName = "ExpireWise";

    [ObservableProperty]
    private int _warningThresholdDays = 30;

    [ObservableProperty]
    private int _criticalThresholdDays = 7;

    [ObservableProperty]
    private string _defaultImportPath = string.Empty;

    [ObservableProperty]
    private string _defaultExportPath = string.Empty;

    [ObservableProperty]
    private int _autoRefreshIntervalMinutes = 0;

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private bool _showExpirationNotifications = true;

    [ObservableProperty]
    private bool _autoLoadLastSession = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ExpireWiseSettingsViewModel(SettingsService settingsService)
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
            var settings = await _settingsService.LoadSettingsAsync<ExpireWiseSettings>(_appName);

            WarningThresholdDays = settings.WarningThresholdDays;
            CriticalThresholdDays = settings.CriticalThresholdDays;
            DefaultImportPath = settings.DefaultImportPath;
            DefaultExportPath = settings.DefaultExportPath;
            AutoRefreshIntervalMinutes = settings.AutoRefreshIntervalMinutes;
            Theme = settings.Theme;
            ShowExpirationNotifications = settings.ShowExpirationNotifications;
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
            var settings = new ExpireWiseSettings
            {
                WarningThresholdDays = WarningThresholdDays,
                CriticalThresholdDays = CriticalThresholdDays,
                DefaultImportPath = DefaultImportPath,
                DefaultExportPath = DefaultExportPath,
                AutoRefreshIntervalMinutes = AutoRefreshIntervalMinutes,
                Theme = Theme,
                ShowExpirationNotifications = ShowExpirationNotifications,
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
