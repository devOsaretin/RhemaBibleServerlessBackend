using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class MongoAdminMetricsRepository(IMongoDbService mongo) : IAdminMetricsRepository
{
  public async Task<DashboardAnalyticsDto> GetAnalyticsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
  {
    var usersCollection = mongo.Users;
    var firstDayOfThisMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1);
    var firstDayOfLastMonth = firstDayOfThisMonth.AddMonths(-1);
    var lastDayOfLastMonth = firstDayOfThisMonth.AddDays(-1);

    var totalUsersTask = usersCollection.CountDocumentsAsync(FilterDefinition<User>.Empty, cancellationToken: cancellationToken);
    var totalPremiumUsersTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.In(u => u.SubscriptionType, new[]
      {
        SubscriptionType.Premium,
        SubscriptionType.PremiumMonthly,
        SubscriptionType.PremiumYearly
      }), cancellationToken: cancellationToken);
    var totalFreeUsersTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.Free), cancellationToken: cancellationToken);
    var activeUsersTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.Status, AccountStatus.Active), cancellationToken: cancellationToken);

    var premiumMonthlyUsersTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.PremiumMonthly), cancellationToken: cancellationToken);
    var premiumYearlyUsersTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.PremiumYearly), cancellationToken: cancellationToken);
    var legacyPremiumUsersTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.Premium), cancellationToken: cancellationToken);

    var newUsersThisMonthTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.Gte(u => u.CreatedAt, firstDayOfThisMonth), cancellationToken: cancellationToken);

    var newUsersLastMonthTask = usersCollection.CountDocumentsAsync(
      Builders<User>.Filter.And(
        Builders<User>.Filter.Gte(u => u.CreatedAt, firstDayOfLastMonth),
        Builders<User>.Filter.Lte(u => u.CreatedAt, lastDayOfLastMonth)), cancellationToken: cancellationToken);

    await Task.WhenAll(
      totalUsersTask,
      totalPremiumUsersTask,
      totalFreeUsersTask,
      activeUsersTask,
      newUsersThisMonthTask,
      newUsersLastMonthTask,
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
    var users = mongo.Users;
    var notes = mongo.Notes;
    var verses = mongo.SavedVerses;
    var activities = mongo.RecentActivities;

    var firstDayOfThisMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var firstDayOfLastMonth = firstDayOfThisMonth.AddMonths(-1);
    var thirtyDaysAgo = nowUtc.Date.AddDays(-29);
    var sevenDaysAgo = nowUtc.AddDays(-7);
    var activitySince = nowUtc.AddDays(-30);

    var totalUsersTask = users.CountDocumentsAsync(FilterDefinition<User>.Empty, cancellationToken: cancellationToken);
    var activeUsersTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.Status, AccountStatus.Active), cancellationToken: cancellationToken);
    var suspendedUsersTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.Status, AccountStatus.Suspended), cancellationToken: cancellationToken);
    var emailVerifiedTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.IsEmailVerified, true), cancellationToken: cancellationToken);
    var emailNotVerifiedTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.IsEmailVerified, false), cancellationToken: cancellationToken);

    var totalPremiumTask = users.CountDocumentsAsync(
      Builders<User>.Filter.In(u => u.SubscriptionType, new[]
      {
        SubscriptionType.Premium,
        SubscriptionType.PremiumMonthly,
        SubscriptionType.PremiumYearly
      }), cancellationToken: cancellationToken);
    var totalFreeTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.Free), cancellationToken: cancellationToken);
    var premiumMonthlyTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.PremiumMonthly), cancellationToken: cancellationToken);
    var premiumYearlyTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.PremiumYearly), cancellationToken: cancellationToken);
    var legacyPremiumTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.Premium), cancellationToken: cancellationToken);

    var newThisMonthTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Gte(u => u.CreatedAt, firstDayOfThisMonth), cancellationToken: cancellationToken);
    var newPrevMonthTask = users.CountDocumentsAsync(
      Builders<User>.Filter.And(
        Builders<User>.Filter.Gte(u => u.CreatedAt, firstDayOfLastMonth),
        Builders<User>.Filter.Lt(u => u.CreatedAt, firstDayOfThisMonth)), cancellationToken: cancellationToken);
    var newLast7DaysTask = users.CountDocumentsAsync(
      Builders<User>.Filter.Gte(u => u.CreatedAt, sevenDaysAgo), cancellationToken: cancellationToken);

    var totalNotesTask = notes.CountDocumentsAsync(FilterDefinition<Note>.Empty, cancellationToken: cancellationToken);
    var totalVersesTask = verses.CountDocumentsAsync(FilterDefinition<SavedVerse>.Empty, cancellationToken: cancellationToken);

    var actFilter = Builders<RecentActivity>.Filter.Gte(a => a.CreatedAt, activitySince);
    var activitiesTotalTask = activities.CountDocumentsAsync(actFilter, cancellationToken: cancellationToken);
    var actAiTask = activities.CountDocumentsAsync(
      Builders<RecentActivity>.Filter.And(
        actFilter,
        Builders<RecentActivity>.Filter.Eq(a => a.ActivityType, ActivityType.AIAnalysis)), cancellationToken: cancellationToken);
    var actNoteTask = activities.CountDocumentsAsync(
      Builders<RecentActivity>.Filter.And(
        actFilter,
        Builders<RecentActivity>.Filter.Eq(a => a.ActivityType, ActivityType.AddNote)), cancellationToken: cancellationToken);
    var actReadTask = activities.CountDocumentsAsync(
      Builders<RecentActivity>.Filter.And(
        actFilter,
        Builders<RecentActivity>.Filter.Eq(a => a.ActivityType, ActivityType.ReadBible)), cancellationToken: cancellationToken);

    var trackedMonthFilter = Builders<User>.Filter.Eq(u => u.AiFreeCallsMonthKey, aiMonthKeyUtc);
    var usersTrackedMonthTask = users.CountDocumentsAsync(trackedMonthFilter, cancellationToken: cancellationToken);
    var usersAtLimitTask = users.CountDocumentsAsync(
      Builders<User>.Filter.And(
        trackedMonthFilter,
        Builders<User>.Filter.Gte(u => u.AiFreeCallsUsedInMonth, freeCallsLimitPerMonth)), cancellationToken: cancellationToken);

    var signupsProject = users.Find(Builders<User>.Filter.Gte(u => u.CreatedAt, thirtyDaysAgo))
      .Project(u => u.CreatedAt);
    var signupsCursorTask = signupsProject.ToListAsync(cancellationToken);

    var usageSumTask = users.Find(trackedMonthFilter)
      .Project(u => u.AiFreeCallsUsedInMonth)
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
