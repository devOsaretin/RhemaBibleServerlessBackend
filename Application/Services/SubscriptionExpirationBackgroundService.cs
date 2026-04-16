using Microsoft.Extensions.DependencyInjection;


public class SubscriptionExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SubscriptionExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

    public SubscriptionExpirationBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SubscriptionExpirationBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription Expiration Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a scope for this execution cycle
                using var scope = _serviceScopeFactory.CreateScope();
                var subscriptionExpirationService = scope.ServiceProvider.GetRequiredService<ISubscriptionExpirationService>();

                _logger.LogInformation("Checking for expired subscriptions...");
                var expiredCount = await subscriptionExpirationService.ExpireSubscriptionsAsync(stoppingToken);

                if (expiredCount > 0)
                {
                    _logger.LogInformation("Processed {Count} expired subscriptions", expiredCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking for expired subscriptions");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Subscription Expiration Background Service stopped");
    }
}

