namespace RhemaBibleAppServerless.Application.Persistence;

public interface INoteRepository
{
  Task InsertAsync(Note note, CancellationToken cancellationToken = default);
  Task<long> DeleteAsync(string userId, string noteId, CancellationToken cancellationToken = default);
  Task<int> DeleteAllByUserAsync(string userId, CancellationToken cancellationToken = default);
  Task<Note?> GetByIdAsync(string noteId, CancellationToken cancellationToken = default);
  Task<PagedResult<Note>> GetPagedByUserAsync(string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
  Task<Note?> UpdateContentAsync(string noteId, string userId, string reference, string text, DateTime updatedAtUtc, CancellationToken cancellationToken = default);
}
