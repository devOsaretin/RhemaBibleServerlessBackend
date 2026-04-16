

public interface IWebHookService
{
    Task<bool> TryMarkEventProcessedAsync(string eventId, CancellationToken ct);
}