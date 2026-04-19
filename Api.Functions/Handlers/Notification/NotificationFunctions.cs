using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Application.Persistence;

public class NotificationFunction(
    INotificationService notificationService,
    IProcessedServiceBusDeliveryRepository processedServiceBusDeliveries,
    ILogger<NotificationFunction> logger)
{

    [Function("Notification")]
    public Task SendEmail(
        [ServiceBusTrigger(QueueNames.Email, Connection = "ServiceBus:ConnectionString")] ServiceBusReceivedMessage message,
        CancellationToken cancellationToken) =>
        FunctionExecutionHelper.ExecuteNonHttpAsync(logger, cancellationToken, async ct =>
        {
            var dedupeKey = ServiceBusDeliveryDedupeKey.Build(QueueNames.Email, message);
            if (!await processedServiceBusDeliveries.TryInsertProcessedDeliveryAsync(dedupeKey, ct))
            {
                logger.LogInformation("Skipping duplicate Service Bus delivery for queue {Queue}", QueueNames.Email);
                return;
            }

            var messageBody = message.Body.ToString();
            var emailRequestFromQueueDto = ParseQueueMessage.Parse<EmailRequestFromQueueDto>(messageBody);
            await notificationService.SendAsync(
                emailRequestFromQueueDto.Recipient,
                emailRequestFromQueueDto.Subject,
                emailRequestFromQueueDto.Body,
                ct);
            logger.LogInformation("Email queue item processed by notification function");
        });
}