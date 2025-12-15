using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SAP.ViewModels;

public partial class UnifiedSettingsViewModel : ObservableObject, IDisposable
{
    public AllocationBuddySettingsViewModel AllocationBuddySettings { get; }
    public EssentialsBuddySettingsViewModel EssentialsBuddySettings { get; }
    public ExpireWiseSettingsViewModel ExpireWiseSettings { get; }
    public DictionaryManagementViewModel DictionaryManagement { get; }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private bool _disposed;

    // Store event handlers so we can unsubscribe later
    private readonly PropertyChangedEventHandler _allocationBuddyHandler;
    private readonly PropertyChangedEventHandler _essentialsBuddyHandler;
    private readonly PropertyChangedEventHandler _expireWiseHandler;
    private readonly PropertyChangedEventHandler _dictionaryHandler;

    public UnifiedSettingsViewModel(
        AllocationBuddySettingsViewModel allocationBuddySettings,
        EssentialsBuddySettingsViewModel essentialsBuddySettings,
        ExpireWiseSettingsViewModel expireWiseSettings,
        DictionaryManagementViewModel dictionaryManagement)
    {
        AllocationBuddySettings = allocationBuddySettings;
        EssentialsBuddySettings = essentialsBuddySettings;
        ExpireWiseSettings = expireWiseSettings;
        DictionaryManagement = dictionaryManagement;

        // Create named handlers so we can unsubscribe
        _allocationBuddyHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(AllocationBuddySettings.StatusMessage))
                StatusMessage = AllocationBuddySettings.StatusMessage;
        };

        _essentialsBuddyHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(EssentialsBuddySettings.StatusMessage))
                StatusMessage = EssentialsBuddySettings.StatusMessage;
        };

        _expireWiseHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(ExpireWiseSettings.StatusMessage))
                StatusMessage = ExpireWiseSettings.StatusMessage;
        };

        _dictionaryHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(DictionaryManagement.StatusMessage))
                StatusMessage = DictionaryManagement.StatusMessage;
        };

        // Subscribe to status message changes from child ViewModels
        AllocationBuddySettings.PropertyChanged += _allocationBuddyHandler;
        EssentialsBuddySettings.PropertyChanged += _essentialsBuddyHandler;
        ExpireWiseSettings.PropertyChanged += _expireWiseHandler;
        DictionaryManagement.PropertyChanged += _dictionaryHandler;
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            AllocationBuddySettings.InitializeAsync(),
            EssentialsBuddySettings.InitializeAsync(),
            ExpireWiseSettings.InitializeAsync(),
            DictionaryManagement.InitializeAsync()
        );
    }

    [RelayCommand]
    private async Task SaveAllSettingsAsync()
    {
        try
        {
            await Task.WhenAll(
                AllocationBuddySettings.SaveSettingsCommand.ExecuteAsync(null),
                EssentialsBuddySettings.SaveSettingsCommand.ExecuteAsync(null),
                ExpireWiseSettings.SaveSettingsCommand.ExecuteAsync(null),
                DictionaryManagement.SaveDictionaryCommand.ExecuteAsync(null)
            );

            StatusMessage = "All settings and dictionary saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetAllToDefaultsAsync()
    {
        try
        {
            await Task.WhenAll(
                AllocationBuddySettings.ResetToDefaultsCommand.ExecuteAsync(null),
                EssentialsBuddySettings.ResetToDefaultsCommand.ExecuteAsync(null),
                ExpireWiseSettings.ResetToDefaultsCommand.ExecuteAsync(null)
            );

            StatusMessage = "All settings reset to defaults";
        }
        catch (Exception ex)
        {
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
            AllocationBuddySettings.PropertyChanged -= _allocationBuddyHandler;
            EssentialsBuddySettings.PropertyChanged -= _essentialsBuddyHandler;
            ExpireWiseSettings.PropertyChanged -= _expireWiseHandler;
            DictionaryManagement.PropertyChanged -= _dictionaryHandler;

            // Dispose child ViewModels if they implement IDisposable
            (DictionaryManagement as IDisposable)?.Dispose();
        }

        _disposed = true;
    }
}
