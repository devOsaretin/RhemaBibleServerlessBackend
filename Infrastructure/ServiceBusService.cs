using System.Text.Json;
using Azure.Messaging.ServiceBus;

public class ServiceBusService(ServiceBusClient serviceBusClient) : IServiceBusService
{
  private static readonly JsonSerializerOptions PublishJsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
  };

  public async Task PublishAsync<T>(T payload, string queueName, CancellationToken cancellationToken)
  {
    ServiceBusSender sender = serviceBusClient.CreateSender(queueName);
    var json = JsonSerializer.Serialize(payload, PublishJsonOptions);
    var message = new ServiceBusMessage(json)
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "application/json"
        };
        await sender.SendMessageAsync(message, cancellationToken);

    }
}