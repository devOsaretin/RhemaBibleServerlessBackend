using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Application.Persistence;

public class SubscriptionExpirationService(
  IUserPersistence users,
  INotificationService notificationService,
  ILogger<SubscriptionExpirationService> logger) : ISubscriptionExpirationService
{
  public async Task<int> ExpireSubscriptionsAsync(CancellationToken cancellationToken = default)
  {
    var now = DateTime.UtcNow;
    var usersToExpire = await users.FindExpiredPremiumSubscriptionsAsync(now, cancellationToken);

    if (usersToExpire.Count == 0)
    {
      logger.LogInformation("No expired subscriptions found to process");
      return 0;
    }

    logger.LogInformation("Found {Count} expired subscriptions to process", usersToExpire.Count);

    var expiredCount = 0;
    foreach (var user in usersToExpire)
    {
      try
      {
        var modified = await users.TryExpirePremiumSubscriptionAsync(user.Id!, now, cancellationToken);

        if (modified)
        {
          expiredCount++;
          logger.LogInformation(
            "Expired subscription for user {UserId} ({Email}). Changed from {OldType} to Free",
            user.Id,
            user.Email,
            user.SubscriptionType);

          _ = Task.Run(async () =>
          {
            try
            {
              var subject = "Rhema Bible — Subscription expired";
              var body = EmailTemplates.SubscriptionExpired();
              await notificationService.SendAsync(user.Email, subject, body, CancellationToken.None);
            }
            catch (Exception ex)
            {
              logger.LogWarning(ex, "Failed to send subscription expired email for user {UserId}", user.Id);
            }
          });
        }
      }
      catch (Exception ex)
      {
        logger.LogError(ex,
          "Failed to expire subscription for user {UserId} ({Email})",
          user.Id,
          user.Email);
      }
    }

    logger.LogInformation("Successfully expired {Count} subscriptions", expiredCount);
    return expiredCount;
  }
}
