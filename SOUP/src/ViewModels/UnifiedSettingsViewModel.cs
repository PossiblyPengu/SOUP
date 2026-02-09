using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SOUP.Services;

namespace SOUP.ViewModels;

public partial class UnifiedSettingsViewModel : ObservableObject, IDisposable
{
    // Application settings (global, not module-specific)
    public ApplicationSettingsViewModel ApplicationSettings { get; }

    // Module settings - nullable for disabled modules
    public AllocationBuddySettingsViewModel? AllocationBuddySettings { get; }
    public EssentialsBuddySettingsViewModel? EssentialsBuddySettings { get; }
    public ExpireWiseSettingsViewModel? ExpireWiseSettings { get; }
    public DictionaryManagementViewModel DictionaryManagement { get; }
    public SOUP.Features.OrderLog.ViewModels.OrderLogViewModel? OrderLogSettings { get; }

    // Module enabled status for UI binding
    public bool IsAllocationBuddyEnabled => ModuleConfiguration.Instance.AllocationBuddyEnabled;
    public bool IsEssentialsBuddyEnabled => ModuleConfiguration.Instance.EssentialsBuddyEnabled;
    public bool IsExpireWiseEnabled => ModuleConfiguration.Instance.ExpireWiseEnabled;
    public bool IsOrderLogEnabled => ModuleConfiguration.Instance.OrderLogEnabled;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private bool _disposed;

    // Store event handlers so we can unsubscribe later
    private readonly PropertyChangedEventHandler _applicationHandler;
    private readonly PropertyChangedEventHandler? _allocationBuddyHandler;
    private readonly PropertyChangedEventHandler? _essentialsBuddyHandler;
    private readonly PropertyChangedEventHandler? _expireWiseHandler;
    private readonly PropertyChangedEventHandler _dictionaryHandler;
    private readonly PropertyChangedEventHandler? _orderLogHandler;

    public UnifiedSettingsViewModel(
        ApplicationSettingsViewModel applicationSettings,
        DictionaryManagementViewModel dictionaryManagement,
        AllocationBuddySettingsViewModel? allocationBuddySettings = null,
        EssentialsBuddySettingsViewModel? essentialsBuddySettings = null,
        ExpireWiseSettingsViewModel? expireWiseSettings = null,
        SOUP.Features.OrderLog.ViewModels.OrderLogViewModel? orderLogSettings = null)
    {
        ApplicationSettings = applicationSettings;
        AllocationBuddySettings = allocationBuddySettings;
        EssentialsBuddySettings = essentialsBuddySettings;
        ExpireWiseSettings = expireWiseSettings;
        DictionaryManagement = dictionaryManagement;
        OrderLogSettings = orderLogSettings;

        // Application settings status passthrough
        _applicationHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(ApplicationSettingsViewModel.StatusMessage))
                StatusMessage = ApplicationSettings.StatusMessage;
        };
        ApplicationSettings.PropertyChanged += _applicationHandler;

        // Create named handlers so we can unsubscribe - only for enabled modules
        if (AllocationBuddySettings != null)
        {
            _allocationBuddyHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(AllocationBuddySettingsViewModel.StatusMessage))
                    StatusMessage = AllocationBuddySettings.StatusMessage;
            };
            AllocationBuddySettings.PropertyChanged += _allocationBuddyHandler;
        }

        if (EssentialsBuddySettings != null)
        {
            _essentialsBuddyHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(EssentialsBuddySettingsViewModel.StatusMessage))
                    StatusMessage = EssentialsBuddySettings.StatusMessage;
            };
            EssentialsBuddySettings.PropertyChanged += _essentialsBuddyHandler;
        }

        if (ExpireWiseSettings != null)
        {
            _expireWiseHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(ExpireWiseSettingsViewModel.StatusMessage))
                    StatusMessage = ExpireWiseSettings.StatusMessage;
            };
            ExpireWiseSettings.PropertyChanged += _expireWiseHandler;
        }

        _dictionaryHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(DictionaryManagement.StatusMessage))
                StatusMessage = DictionaryManagement.StatusMessage;
        };
        DictionaryManagement.PropertyChanged += _dictionaryHandler;

        // OrderLog status passthrough
        if (OrderLogSettings != null)
        {
            _orderLogHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(Features.OrderLog.ViewModels.OrderLogViewModel.StatusMessage))
                    StatusMessage = OrderLogSettings.StatusMessage;
            };
            OrderLogSettings.PropertyChanged += _orderLogHandler;
        }
    }

    public async Task InitializeAsync()
    {
        var tasks = new List<Task> { DictionaryManagement.InitializeAsync() };

        if (AllocationBuddySettings != null)
            tasks.Add(AllocationBuddySettings.InitializeAsync());
        if (EssentialsBuddySettings != null)
            tasks.Add(EssentialsBuddySettings.InitializeAsync());
        if (ExpireWiseSettings != null)
            tasks.Add(ExpireWiseSettings.InitializeAsync());

        await Task.WhenAll(tasks);
    }

    [RelayCommand]
    private async Task SaveAllSettingsAsync()
    {
        try
        {
            var tasks = new List<Task> { DictionaryManagement.SaveDictionaryCommand.ExecuteAsync(null) };

            if (AllocationBuddySettings != null)
                tasks.Add(AllocationBuddySettings.SaveSettingsCommand.ExecuteAsync(null));
            if (EssentialsBuddySettings != null)
                tasks.Add(EssentialsBuddySettings.SaveSettingsCommand.ExecuteAsync(null));
            if (ExpireWiseSettings != null)
                tasks.Add(ExpireWiseSettings.SaveSettingsCommand.ExecuteAsync(null));

            await Task.WhenAll(tasks);

            StatusMessage = "All settings and dictionary saved successfully";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UnifiedSettings: Failed to save all settings");
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetAllToDefaultsAsync()
    {
        try
        {
            var tasks = new List<Task>();

            if (AllocationBuddySettings != null)
                tasks.Add(AllocationBuddySettings.ResetToDefaultsCommand.ExecuteAsync(null));
            if (EssentialsBuddySettings != null)
                tasks.Add(EssentialsBuddySettings.ResetToDefaultsCommand.ExecuteAsync(null));
            if (ExpireWiseSettings != null)
                tasks.Add(ExpireWiseSettings.ResetToDefaultsCommand.ExecuteAsync(null));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);

            StatusMessage = "All settings reset to defaults";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UnifiedSettings: Failed to reset all settings");
            StatusMessage = $"Error resetting settings: {ex.Message}";
        }
    }

    /// <summary>
    /// Dispose pattern to unsubscribe from all event handlers
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe from all child ViewModel events
            ApplicationSettings.PropertyChanged -= _applicationHandler;
            if (AllocationBuddySettings != null && _allocationBuddyHandler != null)
                AllocationBuddySettings.PropertyChanged -= _allocationBuddyHandler;
            if (EssentialsBuddySettings != null && _essentialsBuddyHandler != null)
                EssentialsBuddySettings.PropertyChanged -= _essentialsBuddyHandler;
            if (ExpireWiseSettings != null && _expireWiseHandler != null)
                ExpireWiseSettings.PropertyChanged -= _expireWiseHandler;
            DictionaryManagement.PropertyChanged -= _dictionaryHandler;
            if (OrderLogSettings != null && _orderLogHandler != null)
                OrderLogSettings.PropertyChanged -= _orderLogHandler;

            // Dispose child ViewModels if they implement IDisposable
            (DictionaryManagement as IDisposable)?.Dispose();
        }

        _disposed = true;
    }
}
