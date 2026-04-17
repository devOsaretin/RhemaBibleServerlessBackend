namespace RhemaBibleAppServerless.Application.Configuration;

public sealed class PostgresOptions
{
  public const string SectionName = "Postgres";

  public string ConnectionString { get; set; } = string.Empty;

  public bool ApplyPendingMigrationsOnTimer { get; set; }

  public bool DeleteExpiredOtpRowsOnTimer { get; set; } = true;

  public bool RunLegacySubscriptionEnumFixOnTimer { get; set; } = true;
}
