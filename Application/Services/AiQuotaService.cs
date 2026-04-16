using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Application.Persistence;

public sealed class AiQuotaService : IAiQuotaService
{
  private readonly IUserPersistence _users;
  private readonly Func<DateTime> _utcNow;
  private readonly IOptionsMonitor<AiQuotaOptions> _options;

  public AiQuotaService(
    IUserPersistence users,
    IOptionsMonitor<AiQuotaOptions> options,
    Func<DateTime>? utcNow = null)
  {
    _users = users;
    _options = options;
    _utcNow = utcNow ?? (() => DateTime.UtcNow);
  }

  public int FreeCallsPerMonth => GetEffectiveFreeCallsPerMonth(_options.CurrentValue);

  public static string GetUtcMonthKey(DateTime utcNow) => utcNow.ToString("yyyy-MM");

  public AiUsageDto BuildUsageSnapshot(User user, DateTime? utcNow = null)
  {
    var now = utcNow ?? DateTime.UtcNow;
    if (user.HasActivePremiumSubscription())
      return new AiUsageDto { IsUnlimited = true };

    var limit = GetEffectiveFreeCallsPerMonth(_options.CurrentValue);
    var monthKey = GetUtcMonthKey(now);
    var usedInMonth = string.Equals(user.AiFreeCallsMonthKey, monthKey, StringComparison.Ordinal)
      ? user.AiFreeCallsUsedInMonth
      : 0;

    return new AiUsageDto
    {
      IsUnlimited = false,
      FreeCallsRemainingThisMonth = Math.Max(0, limit - usedInMonth),
      FreeCallsLimitPerMonth = limit,
      FreeCallsUsedThisMonth = usedInMonth,
      MonthKeyUtc = monthKey
    };
  }

  public async Task<AiFreeQuotaConsumeResult> ConsumeFreeCallAsync(string userId, CancellationToken cancellationToken = default)
  {
    var limit = GetEffectiveFreeCallsPerMonth(_options.CurrentValue);
    var monthKey = GetUtcMonthKey(_utcNow());

    var result = await _users.TryConsumeFreeAiCallAsync(userId, limit, monthKey, cancellationToken);
    if (result != null)
      return result;

    throw new AiMonthlyQuotaExceededException(
      $"You've used all {limit} free AI requests this month. Subscribe to keep using AI features.");
  }

  private static int GetEffectiveFreeCallsPerMonth(AiQuotaOptions o)
  {
    var n = o.FreeCallsPerMonth;
    if (n < 1) n = 5;
    if (n > 1_000_000) n = 1_000_000;
    return n;
  }
}
