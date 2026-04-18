using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfNoteRepository(RhemaDbContext db) : INoteRepository
{
  public async Task InsertAsync(Note note, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrEmpty(note.Id))
      note.Id = Guid.NewGuid().ToString("N");
    db.Notes.Add(note);
    await db.SaveChangesAsync(cancellationToken);
  }

  public async Task<long> DeleteAsync(string userId, string noteId, CancellationToken cancellationToken = default)
  {
    var n = await db.Notes.Where(x => x.AuthId == userId && x.Id == noteId).ExecuteDeleteAsync(cancellationToken);
    return n;
  }

  public Task<Note?> GetByIdAsync(string noteId, CancellationToken cancellationToken = default) =>
    db.Notes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == noteId, cancellationToken);

  public async Task<PagedResult<Note>> GetPagedByUserAsync(string userId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
  {
    
    var skip = (pageNumber - 1) * pageSize;
    var q = db.Notes.AsNoTracking().Where(x => x.AuthId == userId);
    var total = await q.LongCountAsync(cancellationToken);
    var items = await q.OrderByDescending(x => x.CreatedAt)
    .Skip(skip)
    .Take(pageSize)
    .ToListAsync(cancellationToken);

    return new PagedResult<Note>
    {
      Items = items,
      TotalItems = total,
      PageNumber = pageNumber,
      PageSize = pageSize
    };
  }

  public async Task<Note?> UpdateContentAsync(string noteId, string userId, string reference, string text, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
  {
    var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.AuthId == userId, cancellationToken);
    if (note == null)
      return null;
    note.Reference = reference;
    note.Text = text;
    note.UpdatedAt = updatedAtUtc;
    await db.SaveChangesAsync(cancellationToken);
    return note;
  }
}
