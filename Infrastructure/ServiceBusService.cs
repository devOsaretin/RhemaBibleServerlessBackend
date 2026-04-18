using System.Text.Json;
using Azure.Messaging.ServiceBus;


public class ServiceBusService(ServiceBusClient serviceBusClient) : IServiceBusService
{

    public async Task PublishAsync<T>(T payload, string queueName, CancellationToken cancellationToken)
    {
        ServiceBusSender sender = serviceBusClient.CreateSender(queueName);
        var json = JsonSerializer.Serialize(payload);
        var message = new ServiceBusMessage(json)
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };
        await sender.SendMessageAsync(message, cancellationToken);

    }
}