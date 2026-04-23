using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Enums;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfAdminMetricsRepository(RhemaDbContext db) : IAdminMetricsRepository
{
  private static readonly SubscriptionType[] PremiumKinds =
  [
    SubscriptionType.Premium,
    SubscriptionType.PremiumMonthly,
    SubscriptionType.PremiumYearly
  ];

  public async Task<DashboardAnalyticsDto> GetAnalyticsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
  {
    var firstDayOfThisMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var users = db.Users.AsNoTracking();

    // One DbContext cannot run multiple queries concurrently; await sequentially.
    var totalUsers = await users.LongCountAsync(cancellationToken);
    var totalPremiumUsers = await users.LongCountAsync(u => PremiumKinds.Contains(u.SubscriptionType), cancellationToken);
    var totalFreeUsers = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Free, cancellationToken);
    var activeUsers = await users.LongCountAsync(u => u.Status == AccountStatus.Active, cancellationToken);
    var newUsersThisMonth = await users.LongCountAsync(u => u.CreatedAt >= firstDayOfThisMonth, cancellationToken);
    var premiumMonthlyUsers = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumMonthly, cancellationToken);
    var premiumYearlyUsers = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumYearly, cancellationToken);
    var legacyPremiumUsers = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Premium, cancellationToken);

    return new DashboardAnalyticsDto(
      TotalUsers: totalUsers,
      TotalPremiumUsers: totalPremiumUsers,
      TotalFreeUsers: totalFreeUsers,
      ActiveUsers: activeUsers,
      NewUsersThisMonth: newUsersThisMonth,
      PremiumMonthlyUsers: premiumMonthlyUsers,
      PremiumYearlyUsers: premiumYearlyUsers,
      LegacyPremiumUsers: legacyPremiumUsers);
  }

  public async Task<DashboardStatisticsRawData> GetStatisticsRawAsync(
    DateTime nowUtc,
    string aiMonthKeyUtc,
    int freeCallsLimitPerMonth,
    CancellationToken cancellationToken = default)
  {
    var users = db.Users.AsNoTracking();
    var notes = db.Notes.AsNoTracking();
    var verses = db.SavedVerses.AsNoTracking();
    var activities = db.RecentActivities.AsNoTracking();

    var firstDayOfThisMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var firstDayOfLastMonth = firstDayOfThisMonth.AddMonths(-1);
    var thirtyDaysAgo = nowUtc.Date.AddDays(-29);
    var sevenDaysAgo = nowUtc.AddDays(-7);
    var activitySince = nowUtc.AddDays(-30);

    // One DbContext cannot run multiple queries concurrently; await sequentially.
    var totalUsers = await users.LongCountAsync(cancellationToken);
    var activeUsers = await users.LongCountAsync(u => u.Status == AccountStatus.Active, cancellationToken);
    var suspendedUsers = await users.LongCountAsync(u => u.Status == AccountStatus.Suspended, cancellationToken);
    var emailVerified = await users.LongCountAsync(u => u.IsEmailVerified, cancellationToken);
    var emailNotVerified = await users.LongCountAsync(u => !u.IsEmailVerified, cancellationToken);

    var totalPremium = await users.LongCountAsync(u => PremiumKinds.Contains(u.SubscriptionType), cancellationToken);
    var totalFree = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Free, cancellationToken);
    var premiumMonthly = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumMonthly, cancellationToken);
    var premiumYearly = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumYearly, cancellationToken);
    var legacyPremium = await users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Premium, cancellationToken);

    var newThisMonth = await users.LongCountAsync(u => u.CreatedAt >= firstDayOfThisMonth, cancellationToken);
    var newPrevMonth = await users.LongCountAsync(
      u => u.CreatedAt >= firstDayOfLastMonth && u.CreatedAt < firstDayOfThisMonth,
      cancellationToken);
    var newLast7Days = await users.LongCountAsync(u => u.CreatedAt >= sevenDaysAgo, cancellationToken);

    var totalNotesCount = await notes.LongCountAsync(cancellationToken);
    var totalVersesCount = await verses.LongCountAsync(cancellationToken);

    var activitiesTotal = await activities.LongCountAsync(a => a.CreatedAt >= activitySince, cancellationToken);
    var actAi = await activities.LongCountAsync(
      a => a.CreatedAt >= activitySince && a.ActivityType == ActivityType.AIAnalysis,
      cancellationToken);
    var actNote = await activities.LongCountAsync(
      a => a.CreatedAt >= activitySince && a.ActivityType == ActivityType.AddNote,
      cancellationToken);
    var actRead = await activities.LongCountAsync(
      a => a.CreatedAt >= activitySince && a.ActivityType == ActivityType.ReadBible,
      cancellationToken);

    var usersTrackedMonth = await users.LongCountAsync(u => u.AiFreeCallsMonthKey == aiMonthKeyUtc, cancellationToken);
    var usersAtLimit = await users.LongCountAsync(
      u => u.AiFreeCallsMonthKey == aiMonthKeyUtc && u.AiFreeCallsUsedInMonth >= freeCallsLimitPerMonth,
      cancellationToken);

    var signupsCursor = await users
      .Where(u => u.CreatedAt >= thirtyDaysAgo)
      .Select(u => u.CreatedAt)
      .ToListAsync(cancellationToken);

    var usageSum = await users
      .Where(u => u.AiFreeCallsMonthKey == aiMonthKeyUtc)
      .Select(u => u.AiFreeCallsUsedInMonth)
      .ToListAsync(cancellationToken);

    return new DashboardStatisticsRawData
    {
      TotalUsers = totalUsers,
      ActiveUsers = activeUsers,
      SuspendedUsers = suspendedUsers,
      EmailVerifiedUsers = emailVerified,
      EmailNotVerifiedUsers = emailNotVerified,
      TotalPremiumUsers = totalPremium,
      TotalFreeUsers = totalFree,
      PremiumMonthlyUsers = premiumMonthly,
      PremiumYearlyUsers = premiumYearly,
      LegacyPremiumUsers = legacyPremium,
      NewUsersThisUtcMonth = newThisMonth,
      NewUsersPreviousUtcMonth = newPrevMonth,
      NewUsersLast7Days = newLast7Days,
      TotalNotes = totalNotesCount,
      TotalSavedVerses = totalVersesCount,
      ActivitiesLast30Days = activitiesTotal,
      AiAnalysisActivitiesLast30Days = actAi,
      AddNoteActivitiesLast30Days = actNote,
      ReadBibleActivitiesLast30Days = actRead,
      UsersWithUsageTrackedThisMonth = usersTrackedMonth,
      UsersAtOrOverFreeLimitThisMonth = usersAtLimit,
      SignupCreatedDatesInWindow = signupsCursor,
      AiFreeCallsUsedInMonthValues = usageSum
    };
  }
}
