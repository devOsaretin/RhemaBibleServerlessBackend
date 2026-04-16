using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

public class WebhookService(IMongoDbService mongoDbService) : IWebHookService
{
    private readonly IMongoCollection<ProcessedWebhook> _processedWebhooks = mongoDbService.ProcessedWebhook;
    public async Task<bool> TryMarkEventProcessedAsync(string eventId, CancellationToken ct)
    {
        try
        {
            var doc = new ProcessedWebhook
            {
                Id = eventId,
                ProcessedAt = DateTime.UtcNow
            };
            await _processedWebhooks.InsertOneAsync(doc, new InsertOneOptions { }, ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;

        }
    }
}