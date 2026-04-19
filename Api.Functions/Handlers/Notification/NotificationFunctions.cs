using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class NotificationFunction(INotificationService notificationService, ILogger<NotificationFunction> logger)
{

    [Function("Notification")]
    public async Task SendEmail([ServiceBusTrigger(QueueNames.Email, Connection = "ServiceBus:ConnectionString")] string messageBody)
    {
        var emailRequestFromQueueDto = ParseQueueMessage.Parse<EmailRequestFromQueueDto>(messageBody);
        await notificationService
        .SendAsync(emailRequestFromQueueDto.Recipient,
            emailRequestFromQueueDto.Subject,
            emailRequestFromQueueDto.Body
        );
        logger.LogInformation("Email queue item processed by notification function");
    }
}