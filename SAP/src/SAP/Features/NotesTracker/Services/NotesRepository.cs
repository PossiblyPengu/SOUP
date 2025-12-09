using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using SAP.Features.NotesTracker.Models;

namespace SAP.Features.NotesTracker.Services
{
    // Simple LiteDB-backed repository for notes
    public class NotesRepository : INotesService, IDisposable
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<NoteItem> _col;

        public NotesRepository()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = System.IO.Path.Combine(appData, "SAP", "NotesTracker");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "notes.db");
            _db = new LiteDatabase(path);
            _col = _db.GetCollection<NoteItem>("notes");
            _col.EnsureIndex(x => x.VendorName);
            _col.EnsureIndex(x => x.Order);
        }

        public Task<List<NoteItem>> LoadAsync()
        {
            // Return items ordered by the stored Order value so UI reflects persisted ordering
            var items = _col.Query().OrderBy(x => x.Order).ToList();
            return Task.FromResult(items);
        }

        public Task SaveAsync(List<NoteItem> items)
        {
            // Replace collection preserving the Order property supplied by caller
            _col.DeleteAll();
            if (items != null && items.Count > 0)
            {
                _col.InsertBulk(items);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
