using RhemaBibleAppServerless.Application.Persistence;

public class WebhookService(IProcessedWebhookRepository processedWebhooks) : IWebHookService
{
  public Task<bool> TryMarkEventProcessedAsync(string eventId, CancellationToken ct) =>
    processedWebhooks.TryInsertProcessedEventAsync(eventId, ct);
}
