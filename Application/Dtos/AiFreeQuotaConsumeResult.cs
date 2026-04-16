

public sealed record AiFreeQuotaConsumeResult(
    int FreeCallsRemainingThisMonth,
    string MonthKeyUtc,
    int FreeCallsUsedThisMonth);
