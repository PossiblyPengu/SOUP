using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SOUP.Features.OrderLog.Services;

/// <summary>
/// Persists group expand/collapse states for the orders UI.
/// </summary>
public sealed class GroupStateStore
{
    private readonly string _path;
    private readonly ILogger<GroupStateStore>? _logger;
    private Dictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);

    public GroupStateStore(ILogger<GroupStateStore>? logger = null)
    {
        _logger = logger;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "SAP", "OrderLog");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "groups.json");

        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            _states = JsonSerializer.Deserialize<Dictionary<string, bool>>(json)
                      ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load group states from {Path}", _path);
            _states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_states, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save group states to {Path}", _path);
        }
    }

    public bool Get(string? name, bool defaultValue = true)
    {
        if (string.IsNullOrEmpty(name)) return defaultValue;
        return _states.TryGetValue(name, out var value) ? value : defaultValue;
    }

    public void Set(string? name, bool value)
    {
        if (string.IsNullOrEmpty(name)) return;
        _states[name] = value;
        Save();
    }

    public void ResetAll()
    {
        _states.Clear();
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete group states file");
        }
    }
}
