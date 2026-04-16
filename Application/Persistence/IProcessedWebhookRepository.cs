namespace RhemaBibleAppServerless.Application.Persistence;

public interface IProcessedWebhookRepository
{
  Task<bool> TryInsertProcessedEventAsync(string eventId, CancellationToken cancellationToken = default);
}
