using MongoDB.Driver;

public class SubscriptionExpirationService(
    IMongoDbService mongoDbService,
    INotificationService notificationService,
    ILogger<SubscriptionExpirationService> logger) : ISubscriptionExpirationService
{
    public async Task<int> ExpireSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        
        // Find all users with expired time-boxed subscriptions (Premium monthly & yearly)
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.In(u => u.SubscriptionType, new[]
            {
                SubscriptionType.PremiumMonthly,
                SubscriptionType.PremiumYearly
            }),
            Builders<User>.Filter.Lte(u => u.SubscriptionExpiresAt, now),
            Builders<User>.Filter.Ne(u => u.SubscriptionExpiresAt, null)
        );

        var usersToExpire = await mongoDbService.Users.Find(filter).ToListAsync(cancellationToken);

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
                var update = Builders<User>.Update
                    .Set(u => u.SubscriptionType, SubscriptionType.Free)
                    .Set(u => u.SubscriptionExpiresAt, (DateTime?)null)
                    .Set(u => u.UpdatedAt, now);

                var result = await mongoDbService.Users.UpdateOneAsync(
                    u => u.Id == user.Id,
                    update,
                    cancellationToken: cancellationToken);

                if (result.ModifiedCount > 0)
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

