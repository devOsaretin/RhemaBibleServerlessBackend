


public interface ISavedVerseService
{
    Task<SavedVerse?> GetSavedVerseAsync(string id, string userId);
    Task<IReadOnlyList<SavedVerse>> GetSavedVersesAsync(string userId);
    Task<SavedVerse> AddVerseAsync(SavedVerseDto savedVerseDto, string userId);
    Task<bool> DeleteSavedVerseAsync(string id, string userId);

}
