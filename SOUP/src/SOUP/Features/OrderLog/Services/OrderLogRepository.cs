using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// LiteDB-backed repository for orders persistence.
/// </summary>
public sealed class OrderLogRepository : IOrderLogService
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<OrderItem> _collection;
    private readonly ILogger<OrderLogRepository>? _logger;
    private bool _disposed;

    public OrderLogRepository(ILogger<OrderLogRepository>? logger = null)
    {
        _logger = logger;

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "SAP", "OrderLog");
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "orders.db");

            _db = new LiteDatabase(dbPath);
            _collection = _db.GetCollection<OrderItem>("orders");
            _collection.EnsureIndex(x => x.VendorName);
            _collection.EnsureIndex(x => x.Order);

            _logger?.LogInformation("OrderLogRepository initialized at {Path}", dbPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize OrderLogRepository");
            throw;
        }
    }

    public Task<List<OrderItem>> LoadAsync()
    {
        try
        {
            var items = _collection.Query().OrderBy(x => x.Order).ToList();
            _logger?.LogInformation("Loaded {Count} orders", items.Count);
            return Task.FromResult(items);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load orders");
            return Task.FromResult(new List<OrderItem>());
        }
    }

    public Task SaveAsync(List<OrderItem> items)
    {
        try
        {
            _collection.DeleteAll();
            if (items is { Count: > 0 })
            {
                _collection.InsertBulk(items);
            }
            _logger?.LogInformation("Saved {Count} orders", items?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save orders");
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db?.Dispose();
        _logger?.LogInformation("OrderLogRepository disposed");
    }
}
