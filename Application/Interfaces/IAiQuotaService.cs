

public interface IAiQuotaService
{
    /// <summary>Configured free AI calls per UTC month (from <c>AiQuota:FreeCallsPerMonth</c> / env).</summary>
    int FreeCallsPerMonth { get; }

    AiUsageDto BuildUsageSnapshot(User user, DateTime? utcNow = null);

    Task<AiFreeQuotaConsumeResult> ConsumeFreeCallAsync(string userId, CancellationToken cancellationToken = default);
}

