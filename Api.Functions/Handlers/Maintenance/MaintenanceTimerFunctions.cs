using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Infrastructure.Services.Maintenance;

public sealed class MaintenanceTimerFunctions(
  IServiceScopeFactory scopeFactory,
  ILogger<MaintenanceTimerFunctions> logger)
{
  [Function("Timer_SubscriptionExpiration")]
  public async Task RunSubscriptionExpirationAsync(
    [TimerTrigger("0 0 * * * *")] TimerInfo timerInfo,
    CancellationToken cancellationToken)
  {
    try
    {
      using var scope = scopeFactory.CreateScope();
      var subscriptionExpirationService = scope.ServiceProvider.GetRequiredService<ISubscriptionExpirationService>();
      logger.LogInformation("Timer: checking for expired subscriptions...");
      var expiredCount = await subscriptionExpirationService.ExpireSubscriptionsAsync(cancellationToken);
      if (expiredCount > 0)
        logger.LogInformation("Timer: processed {Count} expired subscriptions", expiredCount);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Timer: subscription expiration job failed");
      throw;
    }
  }

  [Function("Timer_DatabaseMaintenance")]
  public async Task RunDatabaseMaintenanceAsync(
    [TimerTrigger("0 0 */6 * * *")] TimerInfo timerInfo,
    CancellationToken cancellationToken)
  {
    try
    {
      using var scope = scopeFactory.CreateScope();
      var maintenance = scope.ServiceProvider.GetRequiredService<PostgresMaintenanceService>();
      await maintenance.RunAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Timer: PostgreSQL maintenance failed");
      throw;
    }
  }
}
