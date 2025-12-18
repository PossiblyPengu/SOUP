using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// LiteDB-backed repository for orders persistence.
/// Uses singleton pattern to prevent multiple connections from conflicting.
/// </summary>
public sealed class OrderLogRepository : IOrderLogService
{
    private static readonly object _lock = new();
    private static OrderLogRepository? _instance;
    private static int _refCount;
    
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<OrderItem> _collection;
    private readonly ILogger<OrderLogRepository>? _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Gets or creates the singleton instance of the repository.
    /// </summary>
    public static OrderLogRepository GetInstance(ILogger<OrderLogRepository>? logger = null)
    {
        lock (_lock)
        {
            if (_instance == null || _instance._disposed)
            {
                _instance = new OrderLogRepository(logger);
            }
            _refCount++;
            return _instance;
        }
    }

    private OrderLogRepository(ILogger<OrderLogRepository>? logger = null)
    {
        _logger = logger;

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "SOUP", "OrderLog");
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "orders.db");

            // Use direct connection mode (singleton handles concurrency via semaphore)
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

    public async Task<List<OrderItem>> LoadAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var items = _collection.Query().OrderBy(x => x.Order).ToList();
            _logger?.LogInformation("Loaded {Count} orders", items.Count);
            return items;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load orders");
            return new List<OrderItem>();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(List<OrderItem> items)
    {
        await _semaphore.WaitAsync();
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
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _refCount--;
            if (_refCount > 0) return;
            
            if (_disposed) return;
            _disposed = true;
            _semaphore.Dispose();
            _db?.Dispose();
            _instance = null;
            _logger?.LogInformation("OrderLogRepository disposed");
        }
    }
}
