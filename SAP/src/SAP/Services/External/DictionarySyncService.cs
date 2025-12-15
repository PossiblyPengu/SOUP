using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SAP.Data;
using SAP.Data.Entities;

namespace SAP.Services.External;

/// <summary>
/// Synchronization service for pulling item and store dictionaries from external sources
/// </summary>
public class DictionarySyncService
{
    private readonly MySqlDataService _mySqlService;
    private readonly BusinessCentralService _bcService;
    private readonly ILogger<DictionarySyncService>? _logger;
    
    public event EventHandler<SyncProgressEventArgs>? ProgressChanged;
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    public DictionarySyncService(
        MySqlDataService mySqlService,
        BusinessCentralService bcService,
        ILogger<DictionarySyncService>? logger = null)
    {
        _mySqlService = mySqlService;
        _bcService = bcService;
        _logger = logger;
    }

    /// <summary>
    /// Sync items and stores from MySQL
    /// </summary>
    public async Task<SyncResult> SyncFromMySqlAsync(ExternalConnectionConfig config)
    {
        var result = new SyncResult { Source = "MySQL" };
        
        try
        {
            ReportProgress("Connecting to MySQL...", 0);
            
            if (!await _mySqlService.ConnectAsync(config.GetMySqlConnectionString()))
            {
                result.Success = false;
                result.ErrorMessage = "Failed to connect to MySQL";
                return result;
            }

            // Sync items
            ReportProgress("Loading items from MySQL...", 20);
            var mysqlItems = await _mySqlService.GetItemsAsync();
            var skuMap = await _mySqlService.GetItemSkusAsync();
            
            ReportProgress("Updating local item dictionary...", 40);
            var itemsUpdated = UpdateItemsFromMySql(mysqlItems, skuMap);
            result.ItemsUpdated = itemsUpdated;

            // Sync stores
            ReportProgress("Loading stores from MySQL...", 60);
            var mysqlStores = await _mySqlService.GetStoresAsync();
            
            ReportProgress("Updating local store dictionary...", 80);
            var storesUpdated = UpdateStoresFromMySql(mysqlStores);
            result.StoresUpdated = storesUpdated;

            result.Success = true;
            config.LastSyncTime = DateTime.Now;
            config.Save();
            
            ReportProgress("Sync complete!", 100);
            _logger?.LogInformation("MySQL sync complete: {Items} items, {Stores} stores", itemsUpdated, storesUpdated);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MySQL sync failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        OnSyncCompleted(result);
        return result;
    }

    /// <summary>
    /// Sync items and stores from Business Central
    /// </summary>
    public async Task<SyncResult> SyncFromBusinessCentralAsync(ExternalConnectionConfig config)
    {
        var result = new SyncResult { Source = "Business Central" };
        
        try
        {
            ReportProgress("Authenticating with Business Central...", 0);
            
            var (testSuccess, testMessage) = await _bcService.TestConnectionAsync(config);
            if (!testSuccess)
            {
                result.Success = false;
                result.ErrorMessage = $"Authentication failed: {testMessage}";
                return result;
            }

            // Sync items
            ReportProgress("Loading items from Business Central...", 20);
            var bcItems = await _bcService.GetItemsAsync(config);
            var itemVendors = await _bcService.GetItemVendorsAsync(config);
            
            ReportProgress("Updating local item dictionary...", 40);
            var itemsUpdated = UpdateItemsFromBc(bcItems, itemVendors);
            result.ItemsUpdated = itemsUpdated;

            // Sync locations
            ReportProgress("Loading locations from Business Central...", 60);
            var bcLocations = await _bcService.GetLocationsAsync(config);
            
            ReportProgress("Updating local store dictionary...", 80);
            var storesUpdated = UpdateStoresFromBc(bcLocations);
            result.StoresUpdated = storesUpdated;

            result.Success = true;
            config.LastSyncTime = DateTime.Now;
            config.Save();
            
            ReportProgress("Sync complete!", 100);
            _logger?.LogInformation("BC sync complete: {Items} items, {Stores} stores", itemsUpdated, storesUpdated);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Business Central sync failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        OnSyncCompleted(result);
        return result;
    }

    /// <summary>
    /// Full sync from both sources (MySQL is primary, BC supplements)
    /// </summary>
    public async Task<SyncResult> SyncFromBothAsync(ExternalConnectionConfig config)
    {
        var result = new SyncResult { Source = "MySQL + Business Central" };
        
        try
        {
            // Start with MySQL as primary source
            if (config.IsMySqlConfigured)
            {
                var mysqlResult = await SyncFromMySqlAsync(config);
                result.ItemsUpdated += mysqlResult.ItemsUpdated;
                result.StoresUpdated += mysqlResult.StoresUpdated;
                
                if (!mysqlResult.Success)
                {
                    result.ErrorMessage = $"MySQL: {mysqlResult.ErrorMessage}";
                }
            }

            // Supplement with BC data
            if (config.IsBusinessCentralConfigured)
            {
                var bcResult = await SyncFromBusinessCentralAsync(config);
                result.ItemsUpdated += bcResult.ItemsUpdated;
                result.StoresUpdated += bcResult.StoresUpdated;
                
                if (!bcResult.Success && !string.IsNullOrEmpty(bcResult.ErrorMessage))
                {
                    result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage) 
                        ? $"BC: {bcResult.ErrorMessage}"
                        : $"{result.ErrorMessage}; BC: {bcResult.ErrorMessage}";
                }
            }

            result.Success = string.IsNullOrEmpty(result.ErrorMessage);
            
            if (result.Success)
            {
                config.LastSyncTime = DateTime.Now;
                config.Save();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Combined sync failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        OnSyncCompleted(result);
        return result;
    }

    #region Private Methods

    private int UpdateItemsFromMySql(List<MySqlItem> items, Dictionary<string, List<string>> skuMap)
    {
        var db = DictionaryDbContext.Instance;
        var collection = db.Items;
        int count = 0;

        foreach (var item in items)
        {
            var skus = new List<string>();
            
            // Add UPC if available
            if (!string.IsNullOrWhiteSpace(item.Upc))
                skus.Add(item.Upc);
            
            // Add vendor item number
            if (!string.IsNullOrWhiteSpace(item.VendorItemNo))
                skus.Add(item.VendorItemNo);
            
            // Add from SKU map
            if (skuMap.TryGetValue(item.ItemNo, out var mappedSkus))
                skus.AddRange(mappedSkus);
            
            var entity = new DictionaryItemEntity
            {
                Number = item.ItemNo,
                Description = item.Description,
                Skus = skus.Distinct().ToList(),
                IsEssential = item.IsEssential,
                IsPrivateLabel = item.IsPrivateLabel,
                Tags = new List<string>()
            };

            if (!string.IsNullOrWhiteSpace(item.Category))
                entity.Tags.Add(item.Category);

            collection.Upsert(entity);
            count++;
        }

        return count;
    }

    private int UpdateStoresFromMySql(List<MySqlStore> stores)
    {
        var db = DictionaryDbContext.Instance;
        var collection = db.Stores;
        int count = 0;

        foreach (var store in stores)
        {
            var entity = new StoreEntity
            {
                Code = store.Code,
                Name = store.Name,
                Rank = store.Tier
            };

            collection.Upsert(entity);
            count++;
        }

        return count;
    }

    private int UpdateItemsFromBc(List<BcItem> items, List<BcItemVendor> itemVendors)
    {
        var db = DictionaryDbContext.Instance;
        var collection = db.Items;
        int count = 0;

        // Build vendor item number lookup
        var vendorSkuMap = itemVendors
            .GroupBy(v => v.ItemNumber)
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => v.VendorItemNumber).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
            );

        foreach (var item in items.Where(i => !i.Blocked))
        {
            var existing = collection.FindById(item.Number);
            
            var entity = existing ?? new DictionaryItemEntity { Number = item.Number };
            entity.Description = item.DisplayName;
            
            // Merge SKUs from vendor relationships
            if (vendorSkuMap.TryGetValue(item.Number, out var vendorSkus))
            {
                entity.Skus = entity.Skus.Union(vendorSkus).Distinct().ToList();
            }

            collection.Upsert(entity);
            count++;
        }

        return count;
    }

    private int UpdateStoresFromBc(List<BcLocation> locations)
    {
        var db = DictionaryDbContext.Instance;
        var collection = db.Stores;
        int count = 0;

        foreach (var location in locations)
        {
            var existing = collection.FindOne(s => s.Code == location.Code);
            
            var entity = existing ?? new StoreEntity { Code = location.Code };
            entity.Name = location.DisplayName;

            collection.Upsert(entity);
            count++;
        }

        return count;
    }

    private void ReportProgress(string message, int percent)
    {
        ProgressChanged?.Invoke(this, new SyncProgressEventArgs(message, percent));
    }

    private void OnSyncCompleted(SyncResult result)
    {
        SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(result));
    }

    #endregion
}

#region Event Args and Result Types

public class SyncProgressEventArgs : EventArgs
{
    public string Message { get; }
    public int ProgressPercent { get; }

    public SyncProgressEventArgs(string message, int progressPercent)
    {
        Message = message;
        ProgressPercent = progressPercent;
    }
}

public class SyncCompletedEventArgs : EventArgs
{
    public SyncResult Result { get; }

    public SyncCompletedEventArgs(SyncResult result)
    {
        Result = result;
    }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string Source { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public int ItemsUpdated { get; set; }
    public int StoresUpdated { get; set; }
}

#endregion
