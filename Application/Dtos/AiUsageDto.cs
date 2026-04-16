

public sealed class AiUsageDto
{
    public bool IsUnlimited { get; init; }

    public int? FreeCallsRemainingThisMonth { get; init; }

    public int? FreeCallsLimitPerMonth { get; init; }

    public int? FreeCallsUsedThisMonth { get; init; }

    public string? MonthKeyUtc { get; init; }
}
