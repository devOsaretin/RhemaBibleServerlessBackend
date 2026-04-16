using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class MongoNoteRepository(IMongoDbService mongo) : INoteRepository
{
  public Task InsertAsync(Note note, CancellationToken cancellationToken = default) =>
    mongo.Notes.InsertOneAsync(note, cancellationToken: cancellationToken);

  public async Task<long> DeleteAsync(string userId, string noteId, CancellationToken cancellationToken = default)
  {
    var result = await mongo.Notes.DeleteOneAsync(x => x.AuthId == userId && x.Id == noteId, cancellationToken);
    return result.DeletedCount;
  }

  public async Task<Note?> GetByIdAsync(string noteId, CancellationToken cancellationToken = default)
  {
    return await mongo.Notes.Find(x => x.Id == noteId).FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<PagedResult<Note>> GetPagedByUserAsync(string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
  {
    var skip = (pageNumber - 1) * pageSize;
    var filter = Builders<Note>.Filter.Eq(x => x.AuthId, userId);
    var sort = Builders<Note>.Sort.Descending(x => x.CreatedAt);
    var documentCountTask = mongo.Notes.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    var itemsTask = mongo.Notes.Find(filter).Sort(sort).Skip(skip).Limit(pageSize).ToListAsync(cancellationToken);
    await Task.WhenAll(documentCountTask, itemsTask);
    return new PagedResult<Note>
    {
      Items = itemsTask.Result,
      TotalItems = documentCountTask.Result,
      PageNumber = pageNumber,
      PageSize = pageSize
    };
  }

  public async Task<Note?> UpdateContentAsync(string noteId, string userId, string reference, string text, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
  {
    var filter = Builders<Note>.Filter.And(
      Builders<Note>.Filter.Eq(n => n.Id, noteId),
      Builders<Note>.Filter.Eq(n => n.AuthId, userId));
    var update = Builders<Note>.Update
      .Set(n => n.Reference, reference)
      .Set(n => n.Text, text)
      .Set(n => n.UpdatedAt, updatedAtUtc);
    var options = new FindOneAndUpdateOptions<Note> { ReturnDocument = ReturnDocument.After };
    return await mongo.Notes.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
  }
}
