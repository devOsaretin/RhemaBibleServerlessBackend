using Microsoft.Extensions.Caching.Memory;
using RhemaBibleAppServerless.Application.Persistence;

public class NoteService(
  INoteRepository notes,
  IRecentActivityService recentActivityService,
  IMemoryCache memoryCache,
  IUserResourceEpochStore epochStore) : INoteService
{
  public async Task<Note> CreateNewNoteAsync(Note note)
  {
    await notes.InsertAsync(note, CancellationToken.None);
    epochStore.BumpNotes(note.AuthId);

    _ = Task.Run(async () =>
    {
      try
      {
        var activity = new RecentActivity
        {
          AuthId = note.AuthId,
          ActivityType = ActivityType.AddNote,
          Title = $"Added note to {note.Reference}"
        };

        await recentActivityService.AddActivityByUser(activity);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[Warning] Failed to log activity: {ex.Message}");
      }
    });

    return note;
  }

  public async Task<bool> DeleteNoteAsync(string userId, string noteId)
  {
    var deleted = await notes.DeleteAsync(userId, noteId, CancellationToken.None);
    if (deleted > 0)
      epochStore.BumpNotes(userId);
    return deleted > 0;
  }

  public Task<Note?> GetNoteAsync(string noteId) =>
    notes.GetByIdAsync(noteId, CancellationToken.None);

  public async Task<PagedResult<Note>> GetNotesAsync(string userId, int pageNumber = 1, int pageSize = 10)
  {
    var epoch = epochStore.GetNotesEpoch(userId);
    var cacheKey = $"notes:v1:{userId}:{epoch}:{pageNumber}:{pageSize}";

    return (await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
    {
      entry.SetSlidingExpiration(TimeSpan.FromMinutes(1));
      return await notes.GetPagedByUserAsync(userId, pageNumber, pageSize, CancellationToken.None);
    }))!;
  }

  public async Task<Note?> UpdateNoteAsync(string noteId, string userId, Note updatedNote)
  {
    var result = await notes.UpdateContentAsync(
      noteId,
      userId,
      updatedNote.Reference,
      updatedNote.Text,
      DateTime.UtcNow,
      CancellationToken.None);
    if (result != null)
      epochStore.BumpNotes(userId);
    return result;
  }
}
