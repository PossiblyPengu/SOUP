using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SAP.Core.Entities.Settings;
using SAP.Core.Interfaces;
using SAP.Infrastructure.Services;

namespace SAP.ViewModels;

public partial class ExpireWiseSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly IExpireWiseRepository _repository;
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
    private string _defaultStatusFilter = "All";

    [ObservableProperty]
    private string _dateDisplayFormat = "Short";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _totalItemCount;

    public ExpireWiseSettingsViewModel(SettingsService settingsService, IExpireWiseRepository repository)
    {
        _settingsService = settingsService;
        _repository = repository;
    }

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync().ConfigureAwait(false);
        await RefreshItemCountAsync().ConfigureAwait(false);
    }

    private async Task RefreshItemCountAsync()
    {
        try
        {
            var items = await _repository.GetAllAsync();
            TotalItemCount = System.Linq.Enumerable.Count(items, i => !i.IsDeleted);
        }
        catch
        {
            TotalItemCount = 0;
        }
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
            DefaultStatusFilter = settings.DefaultStatusFilter;
            DateDisplayFormat = settings.DateDisplayFormat;

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
                AutoLoadLastSession = AutoLoadLastSession,
                DefaultStatusFilter = DefaultStatusFilter,
                DateDisplayFormat = DateDisplayFormat
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

    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        // First confirmation
        var result1 = MessageBox.Show(
            $"Are you sure you want to delete ALL {TotalItemCount} expiration items?\n\nThis action cannot be undone.",
            "⚠️ Clear All Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result1 != MessageBoxResult.Yes)
        {
            StatusMessage = "Clear data cancelled";
            return;
        }

        // Second confirmation for safety
        var result2 = MessageBox.Show(
            "This is your FINAL warning!\n\nAll ExpireWise data will be permanently deleted.\n\nAre you absolutely sure?",
            "🚨 Final Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Exclamation);

        if (result2 != MessageBoxResult.Yes)
        {
            StatusMessage = "Clear data cancelled";
            return;
        }

        try
        {
            // Get all items and delete them
            var items = await _repository.GetAllAsync();
            var count = 0;
            foreach (var item in items)
            {
                await _repository.DeleteAsync(item.Id);
                count++;
            }

            await RefreshItemCountAsync();
            StatusMessage = $"✓ Successfully deleted {count} items";
            
            MessageBox.Show(
                $"All {count} expiration items have been deleted.",
                "Data Cleared",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing data: {ex.Message}";
            MessageBox.Show(
                $"Failed to clear data: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
