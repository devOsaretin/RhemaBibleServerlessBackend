using Microsoft.Extensions.Caching.Memory;
using RhemaBibleAppServerless.Application.Persistence;

public class SavedVerseService(
  ISavedVerseRepository verses,
  IMemoryCache memoryCache,
  IUserResourceEpochStore epochStore) : ISavedVerseService
{
  public async Task<SavedVerse> AddVerseAsync(SavedVerseDto savedVerseDto, string userId)
  {
    var verse = await verses.FindByReferenceAndUserAsync(savedVerseDto.Reference, userId, CancellationToken.None);
    if (verse != null)
      return verse;

    var newVerse = new SavedVerse
    {
      Reference = savedVerseDto.Reference,
      Text = savedVerseDto.Text,
      Verse = savedVerseDto.Verse,
      Pilcrow = savedVerseDto.Pilcrow,
      AuthId = userId
    };
    await verses.InsertAsync(newVerse, CancellationToken.None);
    epochStore.BumpSavedVerses(userId);
    return newVerse;
  }

  public async Task<bool> DeleteSavedVerseAsync(string id, string userId)
  {
    var deleted = await verses.DeleteAsync(id, userId, CancellationToken.None);
    if (deleted > 0)
      epochStore.BumpSavedVerses(userId);
    return deleted > 0;
  }

  public Task<SavedVerse?> GetSavedVerseAsync(string id, string userId) =>
    verses.GetByIdAndUserAsync(id, userId, CancellationToken.None);

  public async Task<IReadOnlyList<SavedVerse>> GetSavedVersesAsync(string userId)
  {
    var epoch = epochStore.GetSavedVersesEpoch(userId);
    var cacheKey = $"sv:v1:{userId}:{epoch}";

    return (await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
    {
      entry.SetSlidingExpiration(TimeSpan.FromMinutes(2));
      return await verses.ListByUserAsync(userId, CancellationToken.None);
    }))!;
  }
}
