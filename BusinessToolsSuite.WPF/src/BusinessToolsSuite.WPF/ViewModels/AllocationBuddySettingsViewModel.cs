using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BusinessToolsSuite.Core.Entities.Settings;
using BusinessToolsSuite.Infrastructure.Services;
using BusinessToolsSuite.Infrastructure.Services.Parsers;

namespace BusinessToolsSuite.WPF.ViewModels;

public partial class AllocationBuddySettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly string _appName = "AllocationBuddy";

    [ObservableProperty]
    private string _defaultImportPath = string.Empty;

    [ObservableProperty]
    private string _defaultExportPath = string.Empty;

    [ObservableProperty]
    private string _dictionaryFilePath = string.Empty;

    [ObservableProperty]
    private int _autoSaveIntervalMinutes = 5;

    [ObservableProperty]
    private bool _showConfirmationDialogs = true;

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private bool _autoLoadLastSession = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _newItemNumber = string.Empty;

    [ObservableProperty]
    private string _newItemDescription = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DictionaryItem> _customDictionaryItems = new();

    [ObservableProperty]
    private DictionaryItem? _selectedDictionaryItem;

    public AllocationBuddySettingsViewModel(SettingsService settingsService)
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
            var settings = await _settingsService.LoadSettingsAsync<AllocationBuddySettings>(_appName);

            DefaultImportPath = settings.DefaultImportPath;
            DefaultExportPath = settings.DefaultExportPath;
            DictionaryFilePath = settings.DictionaryFilePath;
            AutoSaveIntervalMinutes = settings.AutoSaveIntervalMinutes;
            ShowConfirmationDialogs = settings.ShowConfirmationDialogs;
            Theme = settings.Theme;
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
            var settings = new AllocationBuddySettings
            {
                DefaultImportPath = DefaultImportPath,
                DefaultExportPath = DefaultExportPath,
                DictionaryFilePath = DictionaryFilePath,
                AutoSaveIntervalMinutes = AutoSaveIntervalMinutes,
                ShowConfirmationDialogs = ShowConfirmationDialogs,
                Theme = Theme,
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
    private void BrowseDictionaryFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select dictionary file",
            Filter = "JavaScript files (*.js)|*.js|All files (*.*)|*.*",
            FileName = DictionaryFilePath
        };

        if (dialog.ShowDialog() == true)
        {
            DictionaryFilePath = dialog.FileName;
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
    private void AddDictionaryItem()
    {
        if (string.IsNullOrWhiteSpace(NewItemNumber))
        {
            StatusMessage = "Please enter an item number";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewItemDescription))
        {
            StatusMessage = "Please enter a description";
            return;
        }

        // Check if item already exists
        if (CustomDictionaryItems.Any(i => i.Number.Equals(NewItemNumber, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Item {NewItemNumber} already exists";
            return;
        }

        var newItem = new DictionaryItem
        {
            Number = NewItemNumber.Trim(),
            Description = NewItemDescription.Trim(),
            Skus = new System.Collections.Generic.List<string>()
        };

        CustomDictionaryItems.Add(newItem);
        StatusMessage = $"Added item {newItem.Number}";

        // Clear input fields
        NewItemNumber = string.Empty;
        NewItemDescription = string.Empty;
    }

    [RelayCommand]
    private void RemoveDictionaryItem(DictionaryItem item)
    {
        if (item != null && CustomDictionaryItems.Contains(item))
        {
            CustomDictionaryItems.Remove(item);
            StatusMessage = $"Removed item {item.Number}";
        }
    }
}
