using Microsoft.Extensions.Caching.Memory;


public class AdminService(
  IUserPersistence users,
  IUserApplicationService userService,
  IMemoryCache memoryCache,
  IAiQuotaService aiQuotaService,
  IAdminMetricsRepository metrics,
  ILogger<AdminService> logger) : IAdminService
{
  public async Task<UserDto> ActivateUserAsync(string userId)
  {
    var updatedUser = await users.UpdateAccountStatusAsync(userId, AccountStatus.Active, CancellationToken.None);
    if (updatedUser == null)
      throw new ResourceNotFoundException($"User with ID '{userId}' was not found.");
    userService.ClearCachedUser(userId);
    return updatedUser.ToDto(aiQuotaService);
  }

  public async Task<UserDto> DeactivateUserAsync(string userId)
  {
    var updatedUser = await users.UpdateAccountStatusAsync(userId, AccountStatus.Suspended, CancellationToken.None);
    if (updatedUser == null)
      throw new ResourceNotFoundException($"User with ID '{userId}' was not found.");
    userService.ClearCachedUser(userId);
    return updatedUser.ToDto(aiQuotaService);
  }

  public Task AdminLoginAsync() => throw new NotImplementedException();

  public Task CreateAdminAsync() => throw new NotImplementedException();

  public Task DisableAdminAsync() => throw new NotImplementedException();

  public async Task<UserDto?> GetAdminAsync(string userId)
  {
    var admin = await users.GetByIdAsync(userId, CancellationToken.None);
    return admin?.ToDto(aiQuotaService);
  }

  public Task GetAllSubscriptionsAsync() => throw new NotImplementedException();

  public async Task<PagedResult<UserDto?>> GetUsersAsync(
    int pageNumber,
    int pageSize,
    string? status,
    string? subscriptionType,
    string? search)
  {
    try
    {
      var query = new AdminUserListQuery
      {
        PageNumber = pageNumber,
        PageSize = pageSize,
        Status = status,
        SubscriptionType = subscriptionType,
        Search = search
      };
      var page = await users.SearchAdminUsersAsync(query, CancellationToken.None);
      var dtos = page.Items.Select(u => (UserDto?)u.ToDto(aiQuotaService)).ToList();
      return new PagedResult<UserDto?>
      {
        Items = dtos,
        TotalItems = page.TotalItems,
        PageNumber = page.PageNumber,
        PageSize = page.PageSize
      };
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error in GetUsersAsync: {ex.Message}");
      throw;
    }
  }

  public async Task<UserDto?> GetUserAsync(string userId, CancellationToken cancellationToken)
  {
    var user = await userService.GetByIdAsync(userId, cancellationToken);
    return user?.ToDto(aiQuotaService);
  }

  public async Task<UserDto> UpdateUsersPlanAsync(string userId, UpdateSubscriptionDto plan, CancellationToken cancellationToken)
  {
    var currentUser = await userService.GetByIdAsync(userId, cancellationToken);
    if (currentUser == null)
      throw new ResourceNotFoundException($"User with ID '{userId}' was not found.");

    var oldSubscriptionType = currentUser.SubscriptionType;
    var newSubscriptionType = plan.SubscriptionType;

    if (oldSubscriptionType != newSubscriptionType)
    {
      logger.LogInformation(
        "Updating subscription for user {UserId}: {OldType} -> {NewType}",
        userId,
        oldSubscriptionType,
        newSubscriptionType);

      if (oldSubscriptionType == SubscriptionType.Premium &&
          (newSubscriptionType == SubscriptionType.PremiumMonthly || newSubscriptionType == SubscriptionType.PremiumYearly))
      {
        logger.LogInformation(
          "Migrating legacy Premium subscription to {NewType} for user {UserId}",
          newSubscriptionType,
          userId);
      }
    }
    else
    {
      logger.LogInformation(
        "Subscription update requested but type unchanged for user {UserId}: {SubscriptionType}",
        userId,
        newSubscriptionType);
    }

    DateTime? expirationDate = plan.SubscriptionExpiresAt;
    if (expirationDate == null)
    {
      if (plan.SubscriptionType is SubscriptionType.PremiumMonthly)
        expirationDate = DateTime.UtcNow.AddMonths(1);
      else if (plan.SubscriptionType is SubscriptionType.PremiumYearly)
        expirationDate = DateTime.UtcNow.AddYears(1);
    }
    else
    {
      if (plan.SubscriptionType is SubscriptionType.Free or SubscriptionType.Premium)
        expirationDate = null;
    }

    var updatedUser = await users.UpdateSubscriptionPlanAsync(
      userId,
      plan.SubscriptionType,
      expirationDate,
      cancellationToken);

    if (updatedUser == null)
    {
      logger.LogError("Failed to update subscription for user {UserId}", userId);
      throw new ResourceNotFoundException($"User with ID '{userId}' was not found or could not be updated.");
    }

    logger.LogInformation(
      "Successfully updated subscription for user {UserId}: {SubscriptionType}",
      userId,
      updatedUser.SubscriptionType);

    userService.ClearCachedUser(userId);
    return updatedUser.ToDto(aiQuotaService);
  }

  public async Task<DashboardAnalyticsDto> GetDashboardAnalyticsAsync()
  {
    const string cacheKey = "dashboard_analytics";
    if (memoryCache.TryGetValue(cacheKey, out DashboardAnalyticsDto? cachedResult) && cachedResult != null)
      return cachedResult;

    var now = DateTime.UtcNow;
    var analytics = await metrics.GetAnalyticsAsync(now, CancellationToken.None);
    memoryCache.Set(cacheKey, analytics, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5)));
    return analytics;
  }

  public async Task<DashboardStatisticsExportDto> GetDashboardStatisticsExportAsync(CancellationToken cancellationToken = default)
  {
    const string cacheKey = "dashboard_statistics_export_v1";
    if (memoryCache.TryGetValue(cacheKey, out DashboardStatisticsExportDto? cached) && cached != null)
      return cached;

    var now = DateTime.UtcNow;
    var monthKey = AiQuotaService.GetUtcMonthKey(now);
    var freeLimit = aiQuotaService.FreeCallsPerMonth;
    var raw = await metrics.GetStatisticsRawAsync(now, monthKey, freeLimit, cancellationToken);

    var thirtyDaysAgo = now.Date.AddDays(-29);
    var byDayDict = raw.SignupCreatedDatesInWindow
      .Select(dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).Date)
      .GroupBy(d => d)
      .ToDictionary(g => g.Key, g => (long)g.Count());

    var signupsSeries = new List<DailySignupCountDto>();
    for (var d = thirtyDaysAgo; d <= now.Date; d = d.AddDays(1))
    {
      var key = d.ToString("yyyy-MM-dd");
      signupsSeries.Add(new DailySignupCountDto(key, byDayDict.GetValueOrDefault(d, 0)));
    }

    double? mom = raw.NewUsersPreviousUtcMonth > 0
      ? (raw.NewUsersThisUtcMonth - raw.NewUsersPreviousUtcMonth) / (double)raw.NewUsersPreviousUtcMonth * 100.0
      : null;

    var export = new DashboardStatisticsExportDto
    {
      GeneratedAtUtc = now,
      Overview = new DashboardOverviewStatsDto
      {
        TotalUsers = raw.TotalUsers,
        ActiveUsers = raw.ActiveUsers,
        SuspendedUsers = raw.SuspendedUsers,
        EmailVerifiedUsers = raw.EmailVerifiedUsers,
        EmailNotVerifiedUsers = raw.EmailNotVerifiedUsers
      },
      Subscriptions = new DashboardSubscriptionBreakdownDto
      {
        TotalPremiumUsers = raw.TotalPremiumUsers,
        TotalFreeUsers = raw.TotalFreeUsers,
        PremiumMonthlyUsers = raw.PremiumMonthlyUsers,
        PremiumYearlyUsers = raw.PremiumYearlyUsers,
        LegacyPremiumUsers = raw.LegacyPremiumUsers,
        PremiumPercentageOfTotal = raw.TotalUsers > 0 ? raw.TotalPremiumUsers / (double)raw.TotalUsers * 100.0 : 0,
        FreePercentageOfTotal = raw.TotalUsers > 0 ? raw.TotalFreeUsers / (double)raw.TotalUsers * 100.0 : 0
      },
      Growth = new DashboardGrowthStatsDto
      {
        NewUsersThisUtcMonth = raw.NewUsersThisUtcMonth,
        NewUsersPreviousUtcMonth = raw.NewUsersPreviousUtcMonth,
        NewUsersLast7Days = raw.NewUsersLast7Days,
        MonthOverMonthNewUserPercentChange = mom
      },
      Content = new DashboardContentStatsDto
      {
        TotalNotes = raw.TotalNotes,
        TotalSavedVerses = raw.TotalSavedVerses
      },
      AiFreeTier = new DashboardAiFreeTierStatsDto
      {
        CurrentUtcMonthKey = monthKey,
        FreeCallsLimitPerMonth = freeLimit,
        UsersWithUsageTrackedThisMonth = raw.UsersWithUsageTrackedThisMonth,
        TotalFreeAiCallsUsedThisMonth = raw.AiFreeCallsUsedInMonthValues.Sum(),
        UsersAtOrOverFreeLimitThisMonth = raw.UsersAtOrOverFreeLimitThisMonth
      },
      Activity = new DashboardActivityStatsDto
      {
        ActivitiesLast30Days = raw.ActivitiesLast30Days,
        AiAnalysisActivitiesLast30Days = raw.AiAnalysisActivitiesLast30Days,
        AddNoteActivitiesLast30Days = raw.AddNoteActivitiesLast30Days,
        ReadBibleActivitiesLast30Days = raw.ReadBibleActivitiesLast30Days
      },
      SignupsLast30DaysByUtcDay = signupsSeries
    };

    memoryCache.Set(cacheKey, export, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(5)));
    return export;
  }

  public async Task<AdminUserAiQuotaDto> GetUserAiQuotaAsync(string userId, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByIdAsync(userId, cancellationToken);
    if (user == null)
      throw new ResourceNotFoundException($"User with ID '{userId}' was not found.");

    return new AdminUserAiQuotaDto
    {
      UserId = userId,
      StoredMonthKey = user.AiFreeCallsMonthKey,
      StoredUsedInMonth = user.AiFreeCallsUsedInMonth,
      AiUsage = aiQuotaService.BuildUsageSnapshot(user)
    };
  }

  public async Task<AdminUserAiQuotaDto> ResetUserAiQuotaAsync(string userId, CancellationToken cancellationToken = default)
  {
    var monthKey = AiQuotaService.GetUtcMonthKey(DateTime.UtcNow);
    var updatedUser = await users.ResetAiQuotaForCurrentMonthAsync(userId, monthKey, cancellationToken);
    if (updatedUser == null)
      throw new ResourceNotFoundException($"User with ID '{userId}' was not found.");

    userService.ClearCachedUser(userId);
    return new AdminUserAiQuotaDto
    {
      UserId = userId,
      StoredMonthKey = updatedUser.AiFreeCallsMonthKey,
      StoredUsedInMonth = updatedUser.AiFreeCallsUsedInMonth,
      AiUsage = aiQuotaService.BuildUsageSnapshot(updatedUser)
    };
  }

  public async Task<AdminUserAiQuotaDto> SetUserAiQuotaRemainingAsync(string userId, int remainingThisMonth, CancellationToken cancellationToken = default)
  {
    remainingThisMonth = Math.Clamp(remainingThisMonth, 0, aiQuotaService.FreeCallsPerMonth);
    var monthKey = AiQuotaService.GetUtcMonthKey(DateTime.UtcNow);
    var used = aiQuotaService.FreeCallsPerMonth - remainingThisMonth;

    var updatedUser = await users.SetAiQuotaUsedForMonthAsync(userId, monthKey, used, cancellationToken);
    if (updatedUser == null)
      throw new ResourceNotFoundException($"User with ID '{userId}' was not found.");

    userService.ClearCachedUser(userId);
    return new AdminUserAiQuotaDto
    {
      UserId = userId,
      StoredMonthKey = updatedUser.AiFreeCallsMonthKey,
      StoredUsedInMonth = updatedUser.AiFreeCallsUsedInMonth,
      AiUsage = aiQuotaService.BuildUsageSnapshot(updatedUser)
    };
  }
}
