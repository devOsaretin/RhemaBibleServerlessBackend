namespace RhemaBibleAppServerless.Application.Persistence;

public interface IProcessedServiceBusDeliveryRepository
{
  Task<bool> TryInsertProcessedDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default);
}
