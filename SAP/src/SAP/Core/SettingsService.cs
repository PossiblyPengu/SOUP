using System;
using System.IO;
using System.Text.Json;
using SAP.Core.Models;

namespace SAP.Core;

public class SettingsService
{
    readonly string _folder;
    readonly string _file;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _folder = Path.Combine(appData, "SAP");
        _file = Path.Combine(_folder, "settings.json");
        if (!Directory.Exists(_folder)) Directory.CreateDirectory(_folder);
    }

    public GameSettings Load()
    {
        try
        {
            if (!File.Exists(_file)) return new GameSettings();
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            return new GameSettings();
        }
    }

    public void Save(GameSettings s)
    {
        try
        {
            var json = JsonSerializer.Serialize(s);
            File.WriteAllText(_file, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
