using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SAP.Features.NotesTracker.Models;

namespace SAP.Features.NotesTracker.Services;

public interface INotesService : IDisposable
{
    Task<List<NoteItem>> LoadAsync();
    Task SaveAsync(List<NoteItem> items);
}
