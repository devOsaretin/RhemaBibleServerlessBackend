using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Application.Persistence;
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

    var totalUsersTask = users.LongCountAsync(cancellationToken);
    var totalPremiumUsersTask = users.LongCountAsync(u => PremiumKinds.Contains(u.SubscriptionType), cancellationToken);
    var totalFreeUsersTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Free, cancellationToken);
    var activeUsersTask = users.LongCountAsync(u => u.Status == AccountStatus.Active, cancellationToken);
    var newUsersThisMonthTask = users.LongCountAsync(u => u.CreatedAt >= firstDayOfThisMonth, cancellationToken);
    var premiumMonthlyUsersTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumMonthly, cancellationToken);
    var premiumYearlyUsersTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumYearly, cancellationToken);
    var legacyPremiumUsersTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Premium, cancellationToken);

    await Task.WhenAll(
      totalUsersTask,
      totalPremiumUsersTask,
      totalFreeUsersTask,
      activeUsersTask,
      newUsersThisMonthTask,
      premiumMonthlyUsersTask,
      premiumYearlyUsersTask,
      legacyPremiumUsersTask);

    return new DashboardAnalyticsDto(
      TotalUsers: totalUsersTask.Result,
      TotalPremiumUsers: totalPremiumUsersTask.Result,
      TotalFreeUsers: totalFreeUsersTask.Result,
      ActiveUsers: activeUsersTask.Result,
      NewUsersThisMonth: newUsersThisMonthTask.Result,
      PremiumMonthlyUsers: premiumMonthlyUsersTask.Result,
      PremiumYearlyUsers: premiumYearlyUsersTask.Result,
      LegacyPremiumUsers: legacyPremiumUsersTask.Result);
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

    var totalUsersTask = users.LongCountAsync(cancellationToken);
    var activeUsersTask = users.LongCountAsync(u => u.Status == AccountStatus.Active, cancellationToken);
    var suspendedUsersTask = users.LongCountAsync(u => u.Status == AccountStatus.Suspended, cancellationToken);
    var emailVerifiedTask = users.LongCountAsync(u => u.IsEmailVerified, cancellationToken);
    var emailNotVerifiedTask = users.LongCountAsync(u => !u.IsEmailVerified, cancellationToken);

    var totalPremiumTask = users.LongCountAsync(u => PremiumKinds.Contains(u.SubscriptionType), cancellationToken);
    var totalFreeTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Free, cancellationToken);
    var premiumMonthlyTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumMonthly, cancellationToken);
    var premiumYearlyTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.PremiumYearly, cancellationToken);
    var legacyPremiumTask = users.LongCountAsync(u => u.SubscriptionType == SubscriptionType.Premium, cancellationToken);

    var newThisMonthTask = users.LongCountAsync(u => u.CreatedAt >= firstDayOfThisMonth, cancellationToken);
    var newPrevMonthTask = users.LongCountAsync(
      u => u.CreatedAt >= firstDayOfLastMonth && u.CreatedAt < firstDayOfThisMonth,
      cancellationToken);
    var newLast7DaysTask = users.LongCountAsync(u => u.CreatedAt >= sevenDaysAgo, cancellationToken);

    var totalNotesTask = notes.LongCountAsync(cancellationToken);
    var totalVersesTask = verses.LongCountAsync(cancellationToken);

    var activitiesTotalTask = activities.LongCountAsync(a => a.CreatedAt >= activitySince, cancellationToken);
    var actAiTask = activities.LongCountAsync(
      a => a.CreatedAt >= activitySince && a.ActivityType == ActivityType.AIAnalysis,
      cancellationToken);
    var actNoteTask = activities.LongCountAsync(
      a => a.CreatedAt >= activitySince && a.ActivityType == ActivityType.AddNote,
      cancellationToken);
    var actReadTask = activities.LongCountAsync(
      a => a.CreatedAt >= activitySince && a.ActivityType == ActivityType.ReadBible,
      cancellationToken);

    var usersTrackedMonthTask = users.LongCountAsync(u => u.AiFreeCallsMonthKey == aiMonthKeyUtc, cancellationToken);
    var usersAtLimitTask = users.LongCountAsync(
      u => u.AiFreeCallsMonthKey == aiMonthKeyUtc && u.AiFreeCallsUsedInMonth >= freeCallsLimitPerMonth,
      cancellationToken);

    var signupsCursorTask = users
      .Where(u => u.CreatedAt >= thirtyDaysAgo)
      .Select(u => u.CreatedAt)
      .ToListAsync(cancellationToken);

    var usageSumTask = users
      .Where(u => u.AiFreeCallsMonthKey == aiMonthKeyUtc)
      .Select(u => u.AiFreeCallsUsedInMonth)
      .ToListAsync(cancellationToken);

    await Task.WhenAll(
      totalUsersTask, activeUsersTask, suspendedUsersTask, emailVerifiedTask, emailNotVerifiedTask,
      totalPremiumTask, totalFreeTask, premiumMonthlyTask, premiumYearlyTask, legacyPremiumTask,
      newThisMonthTask, newPrevMonthTask, newLast7DaysTask,
      totalNotesTask, totalVersesTask,
      activitiesTotalTask, actAiTask, actNoteTask, actReadTask,
      usersTrackedMonthTask, usersAtLimitTask, signupsCursorTask, usageSumTask);

    return new DashboardStatisticsRawData
    {
      TotalUsers = totalUsersTask.Result,
      ActiveUsers = activeUsersTask.Result,
      SuspendedUsers = suspendedUsersTask.Result,
      EmailVerifiedUsers = emailVerifiedTask.Result,
      EmailNotVerifiedUsers = emailNotVerifiedTask.Result,
      TotalPremiumUsers = totalPremiumTask.Result,
      TotalFreeUsers = totalFreeTask.Result,
      PremiumMonthlyUsers = premiumMonthlyTask.Result,
      PremiumYearlyUsers = premiumYearlyTask.Result,
      LegacyPremiumUsers = legacyPremiumTask.Result,
      NewUsersThisUtcMonth = newThisMonthTask.Result,
      NewUsersPreviousUtcMonth = newPrevMonthTask.Result,
      NewUsersLast7Days = newLast7DaysTask.Result,
      TotalNotes = totalNotesTask.Result,
      TotalSavedVerses = totalVersesTask.Result,
      ActivitiesLast30Days = activitiesTotalTask.Result,
      AiAnalysisActivitiesLast30Days = actAiTask.Result,
      AddNoteActivitiesLast30Days = actNoteTask.Result,
      ReadBibleActivitiesLast30Days = actReadTask.Result,
      UsersWithUsageTrackedThisMonth = usersTrackedMonthTask.Result,
      UsersAtOrOverFreeLimitThisMonth = usersAtLimitTask.Result,
      SignupCreatedDatesInWindow = signupsCursorTask.Result,
      AiFreeCallsUsedInMonthValues = usageSumTask.Result
    };
  }
}
