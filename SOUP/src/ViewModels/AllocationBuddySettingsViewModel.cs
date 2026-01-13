using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Serilog;
using SOUP.Core.Entities.Settings;
using SOUP.Data;
using SOUP.Infrastructure.Services;
using SOUP.Infrastructure.Services.Parsers;

namespace SOUP.ViewModels;

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
    private bool _includeDescriptionsInCopy = false;

    [ObservableProperty]
    private string _clipboardFormat = "TabSeparated";

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

    [ObservableProperty]
    private string _newStoreCode = string.Empty;

    [ObservableProperty]
    private string _newStoreName = string.Empty;

    [ObservableProperty]
    private string _newStoreRank = "B";

    [ObservableProperty]
    private ObservableCollection<StoreEntry> _stores = new();

    [ObservableProperty]
    private StoreEntry? _selectedStore;

    public AllocationBuddySettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        // Initialize with empty collection - will be loaded async in InitializeAsync
        Stores = new ObservableCollection<StoreEntry>();
    }

    private async Task LoadStoresFromDictionaryAsync()
    {
        try
        {
            // Load stores from shared dictionary (checks saved file first, then defaults)
            var stores = await Task.Run(() => InternalStoreDictionary.GetStores()).ConfigureAwait(false);
            Stores = new ObservableCollection<StoreEntry>(stores);
        }
        catch (Exception ex)
        {
            // Last resort - empty collection
            Stores = new ObservableCollection<StoreEntry>();
            StatusMessage = $"Error: Could not load stores ({ex.Message})";
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "Loading stores...";
            Log.Information("AllocationBuddySettings: Starting LoadStoresFromDictionaryAsync");
            await LoadStoresFromDictionaryAsync().ConfigureAwait(false);
            StatusMessage = "Stores loaded";
            Log.Information("AllocationBuddySettings: LoadStoresFromDictionaryAsync completed successfully");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading stores: {ex.Message}";
            Log.Error(ex, "AllocationBuddySettings: LoadStoresFromDictionaryAsync failed");
        }

        try
        {
            StatusMessage = "Loading settings...";
            Log.Information("AllocationBuddySettings: Starting LoadSettingsAsync");
            await LoadSettingsAsync().ConfigureAwait(false);
            StatusMessage = "Settings loaded";
            Log.Information("AllocationBuddySettings: LoadSettingsAsync completed successfully");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
            Log.Error(ex, "AllocationBuddySettings: LoadSettingsAsync failed");
        }
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
            IncludeDescriptionsInCopy = settings.IncludeDescriptionsInCopy;
            ClipboardFormat = settings.ClipboardFormat;

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
                AutoLoadLastSession = AutoLoadLastSession,
                IncludeDescriptionsInCopy = IncludeDescriptionsInCopy,
                ClipboardFormat = ClipboardFormat
            };

            await _settingsService.SaveSettingsAsync(_appName, settings);

            // Save stores to a separate file
            await SaveStoresToFileAsync();

            StatusMessage = "Settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    private async Task SaveStoresToFileAsync()
    {
        try
        {
            // Save to shared location used by all apps
            await Task.Run(() => InternalStoreDictionary.SaveStores(Stores.ToList())).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving stores: {ex.Message}";
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

    [RelayCommand]
    private void AddStore()
    {
        if (string.IsNullOrWhiteSpace(NewStoreCode))
        {
            StatusMessage = "Please enter a store code";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewStoreName))
        {
            StatusMessage = "Please enter a store name";
            return;
        }

        // Check if store code already exists
        if (Stores.Any(s => s.Code.Equals(NewStoreCode, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Store code {NewStoreCode} already exists";
            return;
        }

        var newStore = new StoreEntry
        {
            Code = NewStoreCode.Trim(),
            Name = NewStoreName.Trim(),
            Rank = NewStoreRank
        };

        Stores.Add(newStore);
        StatusMessage = $"Added store {newStore.Code} - {newStore.Name}";

        // Clear input fields
        NewStoreCode = string.Empty;
        NewStoreName = string.Empty;
        NewStoreRank = "B";
    }

    [RelayCommand]
    private void RemoveStore(StoreEntry store)
    {
        if (store != null && Stores.Contains(store))
        {
            Stores.Remove(store);
            StatusMessage = $"Removed store {store.Code}";
        }
    }
}
