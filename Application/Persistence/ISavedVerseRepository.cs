namespace RhemaBibleAppServerless.Application.Persistence;

public interface ISavedVerseRepository
{
  Task<SavedVerse?> FindByReferenceAndUserAsync(string reference, string userId, CancellationToken cancellationToken = default);
  Task InsertAsync(SavedVerse verse, CancellationToken cancellationToken = default);
  Task<long> DeleteAsync(string id, string userId, CancellationToken cancellationToken = default);
  Task<SavedVerse?> GetByIdAndUserAsync(string id, string userId, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<SavedVerse>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);
}
