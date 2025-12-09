using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SAP.Features.NotesTracker.Models;

namespace SAP.Features.NotesTracker.Services
{
    public class NotesService : INotesService
    {
        private readonly string _path;

        public NotesService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "SAP", "NotesTracker");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "notes.json");
        }

        public async Task<List<NoteItem>> LoadAsync()
        {
            try
            {
                if (!File.Exists(_path)) return new List<NoteItem>();
                using var fs = File.OpenRead(_path);
                var items = await JsonSerializer.DeserializeAsync<List<NoteItem>>(fs);
                return items ?? new List<NoteItem>();
            }
            catch
            {
                return new List<NoteItem>();
            }
        }

        public async Task SaveAsync(List<NoteItem> items)
        {
            using var fs = File.Create(_path);
            await JsonSerializer.SerializeAsync(fs, items, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
