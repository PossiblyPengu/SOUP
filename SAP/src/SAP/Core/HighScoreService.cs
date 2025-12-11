using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SAP.Core.Models;

namespace SAP.Core;

/// <summary>
/// Simple high-score persistence using JSON in %AppData%\SAP\highscores.json
/// </summary>
public class HighScoreService
{
    readonly string _folder;
    readonly string _file;

    public HighScoreService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _folder = Path.Combine(appData, "SAP");
        _file = Path.Combine(_folder, "highscores.json");
        if (!Directory.Exists(_folder)) Directory.CreateDirectory(_folder);
    }

    public List<HighScoreEntry> LoadTop(int n = 5)
    {
        try
        {
            if (!File.Exists(_file)) return new List<HighScoreEntry>();
            var json = File.ReadAllText(_file);
            var all = JsonSerializer.Deserialize<List<HighScoreEntry>>(json) ?? new List<HighScoreEntry>();
            return all.OrderByDescending(x => x.Score).ThenBy(x => x.Date).Take(n).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load high scores: {ex.Message}");
            return new List<HighScoreEntry>();
        }
    }

    public void AddScore(int score, string? name = null)
    {
        try
        {
            var list = new List<HighScoreEntry>();
            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                list = JsonSerializer.Deserialize<List<HighScoreEntry>>(json) ?? new List<HighScoreEntry>();
            }
            var entry = new HighScoreEntry { Name = string.IsNullOrWhiteSpace(name) ? Environment.UserName.ToUpperInvariant() : name, Score = score, Date = DateTime.UtcNow };
            list.Add(entry);
            var jsonOut = JsonSerializer.Serialize(list);
            File.WriteAllText(_file, jsonOut);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save high score: {ex.Message}");
        }
    }
}
