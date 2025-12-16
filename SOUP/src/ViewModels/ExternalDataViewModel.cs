using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SOUP.Services.External;

namespace SOUP.ViewModels;

/// <summary>
/// ViewModel for external data connection configuration and sync
/// </summary>
public partial class ExternalDataViewModel : ObservableObject
{
    private readonly MySqlDataService _mySqlService;
    private readonly BusinessCentralService _bcService;
    private readonly DictionarySyncService _syncService;
    private readonly ILogger<ExternalDataViewModel>? _logger;
    
    [ObservableProperty]
    private ExternalConnectionConfig _config;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string _syncStatus = "";

    [ObservableProperty]
    private int _syncProgress;

    [ObservableProperty]
    private string? _mySqlTestResult;

    [ObservableProperty]
    private string? _bcTestResult;

    [ObservableProperty]
    private bool _mySqlTestSuccess;

    [ObservableProperty]
    private bool _bcTestSuccess;

    public ExternalDataViewModel(
        MySqlDataService mySqlService,
        BusinessCentralService bcService,
        DictionarySyncService syncService,
        ILogger<ExternalDataViewModel>? logger = null)
    {
        _mySqlService = mySqlService;
        _bcService = bcService;
        _syncService = syncService;
        _logger = logger;
        
        _config = ExternalConnectionConfig.Load();
        
        // Subscribe to sync events
        _syncService.ProgressChanged += (_, e) =>
        {
            SyncStatus = e.Message;
            SyncProgress = e.ProgressPercent;
        };
        
        _syncService.SyncCompleted += (_, e) =>
        {
            IsSyncing = false;
            if (e.Result.Success)
            {
                SyncStatus = $"✓ Synced {e.Result.ItemsUpdated} items, {e.Result.StoresUpdated} stores from {e.Result.Source}";
            }
            else
            {
                SyncStatus = $"✗ Sync failed: {e.Result.ErrorMessage}";
            }
        };
    }

    /// <summary>
    /// Last sync time display
    /// </summary>
    public string LastSyncDisplay => Config.LastSyncTime.HasValue 
        ? $"Last sync: {Config.LastSyncTime:g}" 
        : "Never synced";

    [RelayCommand]
    private async Task TestMySqlConnectionAsync()
    {
        MySqlTestResult = "Testing...";
        MySqlTestSuccess = false;
        
        var (success, message) = await _mySqlService.TestConnectionAsync(Config.GetMySqlConnectionString());
        
        MySqlTestSuccess = success;
        MySqlTestResult = success ? "✓ Connected successfully" : $"✗ {message}";
    }

    [RelayCommand]
    private async Task TestBcConnectionAsync()
    {
        BcTestResult = "Testing...";
        BcTestSuccess = false;
        
        var (success, message) = await _bcService.TestConnectionAsync(Config);
        
        BcTestSuccess = success;
        BcTestResult = success ? "✓ Connected successfully" : $"✗ {message}";
    }

    [RelayCommand]
    private async Task SyncFromMySqlAsync()
    {
        if (IsSyncing) return;
        
        IsSyncing = true;
        SyncProgress = 0;
        
        await _syncService.SyncFromMySqlAsync(Config);
        OnPropertyChanged(nameof(LastSyncDisplay));
    }

    [RelayCommand]
    private async Task SyncFromBcAsync()
    {
        if (IsSyncing) return;
        
        IsSyncing = true;
        SyncProgress = 0;
        
        await _syncService.SyncFromBusinessCentralAsync(Config);
        OnPropertyChanged(nameof(LastSyncDisplay));
    }

    [RelayCommand]
    private async Task SyncFromBothAsync()
    {
        if (IsSyncing) return;
        
        IsSyncing = true;
        SyncProgress = 0;
        
        await _syncService.SyncFromBothAsync(Config);
        OnPropertyChanged(nameof(LastSyncDisplay));
    }

    [RelayCommand]
    private void SaveConfig()
    {
        try
        {
            Config.Save();
            SyncStatus = "✓ Configuration saved";
        }
        catch (Exception ex)
        {
            SyncStatus = $"✗ Failed to save: {ex.Message}";
            _logger?.LogError(ex, "Failed to save external config");
        }
    }
}
