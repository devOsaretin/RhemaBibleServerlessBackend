using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class AccountDeletionFunctions(
  IAccountDeletionService accountDeletionService,
  ILogger<AccountDeletionFunctions> logger)
{
  // Runs every 6 hours. Purges accounts past grace period.
  [Function("AccountDeletion_PurgeExpired")]
  public Task PurgeExpired(
    [TimerTrigger("0 0 */6 * * *")] object timer,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteNonHttpAsync(logger, cancellationToken, async ct =>
    {
      var purged = await accountDeletionService.PurgeExpiredAsync(DateTime.UtcNow, ct);
      logger.LogInformation("AccountDeletion purge complete. Purged={Purged}", purged);
    });
}

