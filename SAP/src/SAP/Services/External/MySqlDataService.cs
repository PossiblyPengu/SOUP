using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Logging;

namespace SAP.Services.External;

/// <summary>
/// Data Access Layer for MySQL database - mirrors SAM's STAG_COMMON pattern.
/// Uses connection-per-operation pattern for better reliability and connection pooling.
/// </summary>
public class MySqlDataService : IDisposable
{
    private readonly ILogger<MySqlDataService>? _logger;
    private string? _connectionString;
    private bool _disposed;

    public MySqlDataService(ILogger<MySqlDataService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Test connection to MySQL server
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            return (true, "Connection successful");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MySQL connection test failed");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Configure the connection string for subsequent operations
    /// </summary>
    public Task<bool> ConnectAsync(string connectionString)
    {
        try
        {
            _connectionString = connectionString;
            _logger?.LogInformation("MySQL connection string configured");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to configure MySQL connection");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Creates a new connection for an operation
    /// </summary>
    private async Task<MySqlConnection?> CreateConnectionAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            _logger?.LogWarning("MySQL connection string not configured");
            return null;
        }

        try
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open MySQL connection");
            return null;
        }
    }

    /// <summary>
    /// Get all items from the STAG items table
    /// </summary>
    public async Task<List<MySqlItem>> GetItemsAsync()
    {
        var items = new List<MySqlItem>();
        
        await using var connection = await CreateConnectionAsync();
        if (connection == null) return items;

        try
        {
            // Query matches SAM's item structure
            const string sql = @"
                SELECT 
                    item_no,
                    description,
                    upc,
                    vendor_no,
                    vendor_item_no,
                    is_essential,
                    is_private_label,
                    category
                FROM items
                ORDER BY item_no";

            await using var cmd = new MySqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var itemNoOrd = reader.GetOrdinal("item_no");
                var descOrd = reader.GetOrdinal("description");
                var upcOrd = reader.GetOrdinal("upc");
                var vendorNoOrd = reader.GetOrdinal("vendor_no");
                var vendorItemNoOrd = reader.GetOrdinal("vendor_item_no");
                var isEssentialOrd = reader.GetOrdinal("is_essential");
                var isPrivateLabelOrd = reader.GetOrdinal("is_private_label");
                var categoryOrd = reader.GetOrdinal("category");
                
                items.Add(new MySqlItem
                {
                    ItemNo = reader.GetString(itemNoOrd),
                    Description = reader.IsDBNull(descOrd) ? "" : reader.GetString(descOrd),
                    Upc = reader.IsDBNull(upcOrd) ? "" : reader.GetString(upcOrd),
                    VendorNo = reader.IsDBNull(vendorNoOrd) ? "" : reader.GetString(vendorNoOrd),
                    VendorItemNo = reader.IsDBNull(vendorItemNoOrd) ? "" : reader.GetString(vendorItemNoOrd),
                    IsEssential = !reader.IsDBNull(isEssentialOrd) && reader.GetBoolean(isEssentialOrd),
                    IsPrivateLabel = !reader.IsDBNull(isPrivateLabelOrd) && reader.GetBoolean(isPrivateLabelOrd),
                    Category = reader.IsDBNull(categoryOrd) ? "" : reader.GetString(categoryOrd)
                });
            }

            _logger?.LogInformation("Loaded {Count} items from MySQL", items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get items from MySQL");
        }

        return items;
    }

    /// <summary>
    /// Get all stores/locations from MySQL
    /// </summary>
    public async Task<List<MySqlStore>> GetStoresAsync()
    {
        var stores = new List<MySqlStore>();
        
        await using var connection = await CreateConnectionAsync();
        if (connection == null) return stores;

        try
        {
            const string sql = @"
                SELECT 
                    store_code,
                    store_name,
                    tier,
                    region,
                    is_active
                FROM stores
                WHERE is_active = 1
                ORDER BY store_code";

            await using var cmd = new MySqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var codeOrd = reader.GetOrdinal("store_code");
                var nameOrd = reader.GetOrdinal("store_name");
                var tierOrd = reader.GetOrdinal("tier");
                var regionOrd = reader.GetOrdinal("region");
                var isActiveOrd = reader.GetOrdinal("is_active");
                
                stores.Add(new MySqlStore
                {
                    Code = reader.GetString(codeOrd),
                    Name = reader.IsDBNull(nameOrd) ? "" : reader.GetString(nameOrd),
                    Tier = reader.IsDBNull(tierOrd) ? "" : reader.GetString(tierOrd),
                    Region = reader.IsDBNull(regionOrd) ? "" : reader.GetString(regionOrd),
                    IsActive = !reader.IsDBNull(isActiveOrd) && reader.GetBoolean(isActiveOrd)
                });
            }

            _logger?.LogInformation("Loaded {Count} stores from MySQL", stores.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get stores from MySQL");
        }

        return stores;
    }

    /// <summary>
    /// Get item SKU mappings (UPC, vendor item numbers)
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetItemSkusAsync()
    {
        var skuMap = new Dictionary<string, List<string>>();
        
        await using var connection = await CreateConnectionAsync();
        if (connection == null) return skuMap;

        try
        {
            const string sql = @"
                SELECT item_no, sku 
                FROM item_skus 
                ORDER BY item_no";

            await using var cmd = new MySqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var itemNoOrd = reader.GetOrdinal("item_no");
                var skuOrd = reader.GetOrdinal("sku");
                var itemNo = reader.GetString(itemNoOrd);
                var sku = reader.GetString(skuOrd);
                
                if (!skuMap.ContainsKey(itemNo))
                    skuMap[itemNo] = new List<string>();
                
                skuMap[itemNo].Add(sku);
            }

            _logger?.LogInformation("Loaded SKU mappings for {Count} items", skuMap.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get item SKUs from MySQL");
        }

        return skuMap;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Connection string only, nothing to dispose with connection-per-operation pattern
            _connectionString = null;
            _disposed = true;
        }
    }
}

/// <summary>
/// Item data from MySQL
/// </summary>
public class MySqlItem
{
    public string ItemNo { get; set; } = "";
    public string Description { get; set; } = "";
    public string Upc { get; set; } = "";
    public string VendorNo { get; set; } = "";
    public string VendorItemNo { get; set; } = "";
    public bool IsEssential { get; set; }
    public bool IsPrivateLabel { get; set; }
    public string Category { get; set; } = "";
}

/// <summary>
/// Store/Location data from MySQL
/// </summary>
public class MySqlStore
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tier { get; set; } = "";
    public string Region { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
