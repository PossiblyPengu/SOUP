using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SAP.Features.NotesTracker.Services
{
    public class GroupStateStore
    {
        private readonly string _path;
        private Dictionary<string, bool> _states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public GroupStateStore()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "SAP", "NotesTracker");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "groups.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var txt = File.ReadAllText(_path);
                _states = JsonSerializer.Deserialize<Dictionary<string, bool>>(txt) ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }
            catch { _states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); }
        }

        private void Save()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_states, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, txt);
            }
            catch { }
        }

        public bool Get(string name, bool defaultValue = true)
        {
            if (name == null) return defaultValue;
            if (_states.TryGetValue(name, out var v)) return v;
            return defaultValue;
        }

        public void Set(string name, bool value)
        {
            if (name == null) return;
            _states[name] = value;
            Save();
        }

        public void ResetAll()
        {
            _states.Clear();
            try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        }
    }
}
