

public interface INoteService
{
    Task<Note> CreateNewNoteAsync(Note note);
    Task<Note?> UpdateNoteAsync(string noteId, string userId, Note updatedNote);

    Task<bool> DeleteNoteAsync(string userId, string noteId);

    Task<Note> GetNoteAsync(string noteId);

    Task<PagedResult<Note>> GetNotesAsync(string userId, int pageNumber, int pageSize);

}
