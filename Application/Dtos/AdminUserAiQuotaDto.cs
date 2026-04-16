

public sealed class AdminUserAiQuotaDto
{
    public string UserId { get; init; } = string.Empty;

    public string? StoredMonthKey { get; init; }

    public int StoredUsedInMonth { get; init; }

    public AiUsageDto AiUsage { get; init; } = null!;
}

