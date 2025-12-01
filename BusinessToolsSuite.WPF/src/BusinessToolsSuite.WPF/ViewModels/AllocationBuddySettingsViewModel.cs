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
using Serilog;

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
            var storesPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BusinessToolsSuite",
                "AllocationBuddy",
                "stores.json"
            );

            if (System.IO.File.Exists(storesPath))
            {
                // Load from saved file asynchronously
                var json = await System.IO.File.ReadAllTextAsync(storesPath);
                var savedStores = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<StoreEntry>>(json);
                if (savedStores != null && savedStores.Count > 0)
                {
                    Stores = new ObservableCollection<StoreEntry>(savedStores);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue to load defaults
            StatusMessage = $"Warning: Could not load custom stores ({ex.Message}), using defaults";
        }

        // Load stores from internal dictionary (defaults)
        try
        {
            // Run on background thread to avoid UI freeze
            var internalStores = await Task.Run(() => BusinessToolsSuite.WPF.Data.InternalStoreDictionary.GetDefaultStores());
            Stores = new ObservableCollection<StoreEntry>(internalStores);
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
            await LoadStoresFromDictionaryAsync();
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
            await LoadSettingsAsync();
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
            var storesPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BusinessToolsSuite",
                "AllocationBuddy",
                "stores.json"
            );

            var directory = System.IO.Path.GetDirectoryName(storesPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory!);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(Stores.ToList(), new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(storesPath, json);
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
