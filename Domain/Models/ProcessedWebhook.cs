namespace RhemaBibleAppServerless.Domain.Models;

public class ProcessedWebhook
{
  public string Id { get; set; } = default!;

  public DateTime ProcessedAt { get; set; }
}
