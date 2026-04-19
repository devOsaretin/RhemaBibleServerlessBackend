namespace RhemaBibleAppServerless.Application.Persistence;

public sealed class DashboardStatisticsRawData
{
  public long TotalUsers { get; init; }
  public long ActiveUsers { get; init; }
  public long SuspendedUsers { get; init; }
  public long EmailVerifiedUsers { get; init; }
  public long EmailNotVerifiedUsers { get; init; }

  public long TotalPremiumUsers { get; init; }
  public long TotalFreeUsers { get; init; }
  public long PremiumMonthlyUsers { get; init; }
  public long PremiumYearlyUsers { get; init; }
  public long LegacyPremiumUsers { get; init; }

  public long NewUsersThisUtcMonth { get; init; }
  public long NewUsersPreviousUtcMonth { get; init; }
  public long NewUsersLast7Days { get; init; }

  public long TotalNotes { get; init; }
  public long TotalSavedVerses { get; init; }

  public long ActivitiesLast30Days { get; init; }
  public long AiAnalysisActivitiesLast30Days { get; init; }
  public long AddNoteActivitiesLast30Days { get; init; }
  public long ReadBibleActivitiesLast30Days { get; init; }

  public long UsersWithUsageTrackedThisMonth { get; init; }
  public long UsersAtOrOverFreeLimitThisMonth { get; init; }

  public IReadOnlyList<DateTime> SignupCreatedDatesInWindow { get; init; } = Array.Empty<DateTime>();
  public IReadOnlyList<int> AiFreeCallsUsedInMonthValues { get; init; } = Array.Empty<int>();
}
