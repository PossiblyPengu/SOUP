using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusinessToolsSuite.WPF.ViewModels;

public partial class UnifiedSettingsViewModel : ObservableObject
{
    public AllocationBuddySettingsViewModel AllocationBuddySettings { get; }
    public EssentialsBuddySettingsViewModel EssentialsBuddySettings { get; }
    public ExpireWiseSettingsViewModel ExpireWiseSettings { get; }
    public DictionaryManagementViewModel DictionaryManagement { get; }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

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

        // Subscribe to status message changes from child ViewModels
        AllocationBuddySettings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AllocationBuddySettings.StatusMessage))
                StatusMessage = AllocationBuddySettings.StatusMessage;
        };

        EssentialsBuddySettings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EssentialsBuddySettings.StatusMessage))
                StatusMessage = EssentialsBuddySettings.StatusMessage;
        };

        ExpireWiseSettings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ExpireWiseSettings.StatusMessage))
                StatusMessage = ExpireWiseSettings.StatusMessage;
        };

        DictionaryManagement.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DictionaryManagement.StatusMessage))
                StatusMessage = DictionaryManagement.StatusMessage;
        };
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
}
