public interface IServiceBusService
{
    Task PublishAsync<T>(T payload, string queueName, CancellationToken cancellationToken = default);
}

