using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class MongoProcessedWebhookRepository(IMongoDbService mongo) : IProcessedWebhookRepository
{
  public async Task<bool> TryInsertProcessedEventAsync(string eventId, CancellationToken cancellationToken = default)
  {
    try
    {
      var doc = new ProcessedWebhook
      {
        Id = eventId,
        ProcessedAt = DateTime.UtcNow
      };
      await mongo.ProcessedWebhook.InsertOneAsync(doc, new InsertOneOptions(), cancellationToken);
      return true;
    }
    catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
    {
      return false;
    }
  }
}
