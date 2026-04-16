using Microsoft.Extensions.Options;
using MongoDB.Driver;

public sealed class AiQuotaService : IAiQuotaService
{
    private readonly IMongoCollection<User> _users;
    private readonly Func<DateTime> _utcNow;
    private readonly IOptionsMonitor<AiQuotaOptions> _options;

    public AiQuotaService(
        IMongoDbService mongoDbService,
        IOptionsMonitor<AiQuotaOptions> options,
        Func<DateTime>? utcNow = null)
        : this(mongoDbService.Users, options, utcNow)
    {
    }

    public AiQuotaService(
        IMongoCollection<User> users,
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
        {
            return new AiUsageDto { IsUnlimited = true };
        }

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

        var filterSameMonthHasQuota =
            Builders<User>.Filter.Eq(u => u.Id, userId) &
            Builders<User>.Filter.Eq(u => u.AiFreeCallsMonthKey, monthKey) &
            Builders<User>.Filter.Lt(u => u.AiFreeCallsUsedInMonth, limit);

        var updateInc = Builders<User>.Update.Inc(u => u.AiFreeCallsUsedInMonth, 1);

        var updated = await _users.FindOneAndUpdateAsync(
            filterSameMonthHasQuota,
            updateInc,
            new FindOneAndUpdateOptions<User>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        if (updated != null)
        {
            return new AiFreeQuotaConsumeResult(
                FreeCallsRemainingThisMonth: Math.Max(0, limit - updated.AiFreeCallsUsedInMonth),
                MonthKeyUtc: monthKey,
                FreeCallsUsedThisMonth: updated.AiFreeCallsUsedInMonth);
        }

        var filterNewMonth =
            Builders<User>.Filter.Eq(u => u.Id, userId) &
            Builders<User>.Filter.Ne(u => u.AiFreeCallsMonthKey, monthKey);

        var updateResetToOne = Builders<User>.Update
            .Set(u => u.AiFreeCallsMonthKey, monthKey)
            .Set(u => u.AiFreeCallsUsedInMonth, 1);

        updated = await _users.FindOneAndUpdateAsync(
            filterNewMonth,
            updateResetToOne,
            new FindOneAndUpdateOptions<User>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        if (updated != null)
        {
            return new AiFreeQuotaConsumeResult(
                FreeCallsRemainingThisMonth: limit - updated.AiFreeCallsUsedInMonth,
                MonthKeyUtc: monthKey,
                FreeCallsUsedThisMonth: updated.AiFreeCallsUsedInMonth);
        }

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

