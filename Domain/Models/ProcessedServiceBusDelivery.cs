namespace RhemaBibleAppServerless.Domain.Models;

public sealed class ProcessedServiceBusDelivery
{
  public string Id { get; set; } = default!;

  public DateTime ProcessedAt { get; set; }
}
