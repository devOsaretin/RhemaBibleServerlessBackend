using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Application.Configuration;
using RhemaBibleAppServerless.Infrastructure.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Services.Maintenance;

public sealed class PostgresMaintenanceService(
  RhemaDbContext db,
  IOptions<PostgresOptions> options,
  ILogger<PostgresMaintenanceService> logger)
{
  private readonly PostgresOptions _options = options.Value;

  public async Task RunAsync(CancellationToken cancellationToken = default)
  {
    if (_options.ApplyPendingMigrationsOnTimer)
    {
      var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
      if (pending.Any())
      {
        logger.LogInformation("Applying {Count} pending EF migrations…", pending.Count());
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("EF migrations applied.");
      }
    }

    if (_options.RunLegacySubscriptionEnumFixOnTimer)
      await RunLegacySubscriptionFixesAsync(cancellationToken);

    if (_options.DeleteExpiredOtpRowsOnTimer)
      await DeleteExpiredOtpsAsync(cancellationToken);
  }

  private async Task RunLegacySubscriptionFixesAsync(CancellationToken cancellationToken)
  {
    var monthly = await db.Database.ExecuteSqlRawAsync(
      "UPDATE users SET subscription_type = 'PremiumMonthly' WHERE subscription_type = 'ProMonthly';",
      cancellationToken);
    var yearly = await db.Database.ExecuteSqlRawAsync(
      "UPDATE users SET subscription_type = 'PremiumYearly' WHERE subscription_type = 'ProYearly';",
      cancellationToken);

    if (monthly > 0 || yearly > 0)
    {
      logger.LogInformation(
        "Legacy subscription string migration: ProMonthly→PremiumMonthly {M}, ProYearly→PremiumYearly {Y}",
        monthly,
        yearly);
    }
  }

  private async Task DeleteExpiredOtpsAsync(CancellationToken cancellationToken)
  {
    var n = await db.Database.ExecuteSqlInterpolatedAsync(
      $"DELETE FROM otp_codes WHERE expires_at < {DateTime.UtcNow}",
      cancellationToken);
    if (n > 0)
      logger.LogInformation("Deleted {Count} expired OTP row(s).", n);
  }
}
