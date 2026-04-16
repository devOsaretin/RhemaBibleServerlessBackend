using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

public class SavedVerseService(
    IMongoDbService mongoDbService,
    IMemoryCache memoryCache,
    IUserResourceEpochStore epochStore) : ISavedVerseService
{
    private readonly IMongoCollection<SavedVerse> _savedVerseCollection = mongoDbService.SavedVerses;
    public async Task<SavedVerse> AddVerseAsync(SavedVerseDto savedVerseDto, string userId)
    {

        var verse = await _savedVerseCollection
        .Find(x => x.Reference == savedVerseDto.Reference
        && x.AuthId == userId)
        .FirstOrDefaultAsync()
        ;
        if (verse != null)
        {
            return verse;
        }

        SavedVerse newVerse = new()
        {
            Reference = savedVerseDto.Reference,
            Text = savedVerseDto.Text,
            Verse = savedVerseDto.Verse,
            Pilcrow = savedVerseDto.Pilcrow,
            AuthId = userId

        };
        await _savedVerseCollection.InsertOneAsync(newVerse);
        epochStore.BumpSavedVerses(userId);
        return newVerse;

    }

    public async Task<bool> DeleteSavedVerseAsync(string id, string userId)
    {

        var result = await _savedVerseCollection.DeleteOneAsync(
            v => v.Id == id && v.AuthId == userId
        );

        if (result.DeletedCount > 0)
            epochStore.BumpSavedVerses(userId);

        return result.DeletedCount > 0;
    }

    public async Task<SavedVerse?> GetSavedVerseAsync(string id, string userId)
    {

        return await _savedVerseCollection
        .Find(x => x.AuthId == userId && x.Id == id).FirstOrDefaultAsync();


    }

    public async Task<IReadOnlyList<SavedVerse>> GetSavedVersesAsync(string userId)
    {
        var epoch = epochStore.GetSavedVersesEpoch(userId);
        var cacheKey = $"sv:v1:{userId}:{epoch}";

        return (await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(2));
            var list = await _savedVerseCollection.Find(x => x.AuthId == userId).ToListAsync();
            return (IReadOnlyList<SavedVerse>)list;
        }))!;
    }
}

