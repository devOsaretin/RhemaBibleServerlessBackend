
public sealed class DashboardStatisticsExportDto
{
    public DateTime GeneratedAtUtc { get; init; }

    public DashboardOverviewStatsDto Overview { get; init; } = null!;
    public DashboardSubscriptionBreakdownDto Subscriptions { get; init; } = null!;
    public DashboardGrowthStatsDto Growth { get; init; } = null!;
    public DashboardContentStatsDto Content { get; init; } = null!;
    public DashboardAiFreeTierStatsDto AiFreeTier { get; init; } = null!;
    public DashboardActivityStatsDto Activity { get; init; } = null!;

    public IReadOnlyList<DailySignupCountDto> SignupsLast30DaysByUtcDay { get; init; } = Array.Empty<DailySignupCountDto>();
}

public sealed class DashboardOverviewStatsDto
{
    public long TotalUsers { get; init; }
    public long ActiveUsers { get; init; }
    public long SuspendedUsers { get; init; }
    public long EmailVerifiedUsers { get; init; }
    public long EmailNotVerifiedUsers { get; init; }
}

public sealed class DashboardSubscriptionBreakdownDto
{
    public long TotalPremiumUsers { get; init; }
    public long TotalFreeUsers { get; init; }
    public long PremiumMonthlyUsers { get; init; }
    public long PremiumYearlyUsers { get; init; }
    public long LegacyPremiumUsers { get; init; }
    public double PremiumPercentageOfTotal { get; init; }
    public double FreePercentageOfTotal { get; init; }
}

public sealed class DashboardGrowthStatsDto
{
    public long NewUsersThisUtcMonth { get; init; }
    public long NewUsersPreviousUtcMonth { get; init; }
    public long NewUsersLast7Days { get; init; }
    public double? MonthOverMonthNewUserPercentChange { get; init; }
}

public sealed class DashboardContentStatsDto
{
    public long TotalNotes { get; init; }
    public long TotalSavedVerses { get; init; }
}

public sealed class DashboardAiFreeTierStatsDto
{
    public string CurrentUtcMonthKey { get; init; } = "";
    public int FreeCallsLimitPerMonth { get; init; }
    public long UsersWithUsageTrackedThisMonth { get; init; }
    public long TotalFreeAiCallsUsedThisMonth { get; init; }
    public long UsersAtOrOverFreeLimitThisMonth { get; init; }
}

public sealed class DashboardActivityStatsDto
{
    public long ActivitiesLast30Days { get; init; }
    public long AiAnalysisActivitiesLast30Days { get; init; }
    public long AddNoteActivitiesLast30Days { get; init; }
    public long ReadBibleActivitiesLast30Days { get; init; }
}

public sealed record DailySignupCountDto(string UtcDateKey, long Count);
