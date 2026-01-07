using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SOUP.Services;

/// <summary>
/// Service for persisting sidebar navigation order.
/// </summary>
public class NavOrderService
{
    private readonly string _filePath;
    private readonly ILogger<NavOrderService>? _logger;
    private Dictionary<string, int> _orderMap = new();

    public NavOrderService(ILogger<NavOrderService>? logger = null)
    {
        _logger = logger;

        _filePath = Path.Combine(Core.AppPaths.AppData, "nav-order.json");

        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            _orderMap = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
            _logger?.LogInformation("Loaded nav order with {Count} items", _orderMap.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load nav order from {Path}", _filePath);
            _orderMap = new();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_orderMap, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            _logger?.LogInformation("Saved nav order");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save nav order to {Path}", _filePath);
        }
    }

    /// <summary>
    /// Gets the order index for a nav item. Returns defaultOrder if not found.
    /// </summary>
    public int GetOrder(string id, int defaultOrder)
    {
        return _orderMap.TryGetValue(id, out var order) ? order : defaultOrder;
    }

    /// <summary>
    /// Sets the order index for a nav item.
    /// </summary>
    public void SetOrder(string id, int order)
    {
        _orderMap[id] = order;
    }

    /// <summary>
    /// Updates order for all items based on their position in the list.
    /// </summary>
    public void UpdateOrder(IEnumerable<string> orderedIds)
    {
        var index = 0;
        foreach (var id in orderedIds)
        {
            _orderMap[id] = index++;
        }
        Save();
    }
}
