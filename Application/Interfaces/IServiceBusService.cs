public interface IServiceBusService
{
    Task PublishAsync<T>(T payload, CancellationToken cancellationToken);
}

