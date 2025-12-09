using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using SAP.Features.NotesTracker.Models;

namespace SAP.Features.NotesTracker.Services;

/// <summary>
/// LiteDB-backed repository for notes persistence.
/// </summary>
public sealed class NotesRepository : INotesService
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<NoteItem> _collection;
    private readonly ILogger<NotesRepository>? _logger;
    private bool _disposed;

    public NotesRepository(ILogger<NotesRepository>? logger = null)
    {
        _logger = logger;

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "SAP", "NotesTracker");
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "notes.db");

            _db = new LiteDatabase(dbPath);
            _collection = _db.GetCollection<NoteItem>("notes");
            _collection.EnsureIndex(x => x.VendorName);
            _collection.EnsureIndex(x => x.Order);

            _logger?.LogInformation("NotesRepository initialized at {Path}", dbPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize NotesRepository");
            throw;
        }
    }

    public Task<List<NoteItem>> LoadAsync()
    {
        try
        {
            var items = _collection.Query().OrderBy(x => x.Order).ToList();
            _logger?.LogInformation("Loaded {Count} notes", items.Count);
            return Task.FromResult(items);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load notes");
            return Task.FromResult(new List<NoteItem>());
        }
    }

    public Task SaveAsync(List<NoteItem> items)
    {
        try
        {
            _collection.DeleteAll();
            if (items is { Count: > 0 })
            {
                _collection.InsertBulk(items);
            }
            _logger?.LogInformation("Saved {Count} notes", items?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save notes");
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _db?.Dispose();
        _logger?.LogInformation("NotesRepository disposed");
    }
}
