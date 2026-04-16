using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class MongoSavedVerseRepository(IMongoDbService mongo) : ISavedVerseRepository
{
  public async Task<SavedVerse?> FindByReferenceAndUserAsync(string reference, string userId, CancellationToken cancellationToken = default)
  {
    return await mongo.SavedVerses.Find(x => x.Reference == reference && x.AuthId == userId).FirstOrDefaultAsync(cancellationToken);
  }

  public Task InsertAsync(SavedVerse verse, CancellationToken cancellationToken = default) =>
    mongo.SavedVerses.InsertOneAsync(verse, cancellationToken: cancellationToken);

  public async Task<long> DeleteAsync(string id, string userId, CancellationToken cancellationToken = default)
  {
    var result = await mongo.SavedVerses.DeleteOneAsync(v => v.Id == id && v.AuthId == userId, cancellationToken);
    return result.DeletedCount;
  }

  public async Task<SavedVerse?> GetByIdAndUserAsync(string id, string userId, CancellationToken cancellationToken = default)
  {
    return await mongo.SavedVerses.Find(x => x.AuthId == userId && x.Id == id).FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<SavedVerse>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
  {
    var list = await mongo.SavedVerses.Find(x => x.AuthId == userId).ToListAsync(cancellationToken);
    return list;
  }
}
