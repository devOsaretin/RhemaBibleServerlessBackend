using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfSavedVerseRepository(RhemaDbContext db) : ISavedVerseRepository
{
  public Task<SavedVerse?> FindByReferenceAndUserAsync(string reference, string userId, CancellationToken cancellationToken = default) =>
    db.SavedVerses.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == reference && x.AuthId == userId, cancellationToken);

  public async Task InsertAsync(SavedVerse verse, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrEmpty(verse.Id))
      verse.Id = Guid.NewGuid().ToString("N");
    db.SavedVerses.Add(verse);
    await db.SaveChangesAsync(cancellationToken);
  }

  public async Task<long> DeleteAsync(string id, string userId, CancellationToken cancellationToken = default)
  {
    var n = await db.SavedVerses.Where(v => v.Id == id && v.AuthId == userId).ExecuteDeleteAsync(cancellationToken);
    return n;
  }

  public Task<int> DeleteAllByUserAsync(string userId, CancellationToken cancellationToken = default) =>
    db.SavedVerses.Where(v => v.AuthId == userId).ExecuteDeleteAsync(cancellationToken);

  public Task<SavedVerse?> GetByIdAndUserAsync(string id, string userId, CancellationToken cancellationToken = default) =>
    db.SavedVerses.AsNoTracking().FirstOrDefaultAsync(x => x.AuthId == userId && x.Id == id, cancellationToken);

  public async Task<IReadOnlyList<SavedVerse>> ListByUserAsync(string userId, CancellationToken cancellationToken = default) =>
    await db.SavedVerses.AsNoTracking().Where(x => x.AuthId == userId).ToListAsync(cancellationToken);
}
