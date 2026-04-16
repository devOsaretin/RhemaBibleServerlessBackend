using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;

public class AdminService(
    IMongoDbService mongoDbService,
    IUserService userService,
    IMemoryCache memoryCache,
    IAiQuotaService aiQuotaService,
    ILogger<AdminService> logger) : IAdminService
{
    public async Task<UserDto> ActivateUserAsync(string userId)
    {
        var update = Builders<User>.Update
        .Set(x => x.Status, AccountStatus.Active)
        .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<User>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updatedUser = await mongoDbService.Users.FindOneAndUpdateAsync(
            Builders<User>.Filter.Eq(x => x.Id, userId),
            update,
            options
        );

        return updatedUser.ToDto(aiQuotaService);
    }

    public async Task<UserDto> DeactivateUserAsync(string userId)
    {
        var update = Builders<User>.Update
        .Set(x => x.Status, AccountStatus.Suspended)
        .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<User>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updatedUser = await mongoDbService.Users.FindOneAndUpdateAsync(
            Builders<User>.Filter.Eq(x => x.Id, userId),
            update,
            options
        );

        return updatedUser.ToDto(aiQuotaService);
    }

    public Task AdminLoginAsync()
    {
        throw new NotImplementedException();
    }

    public Task CreateAdminAsync()
    {
        throw new NotImplementedException();
    }

    public Task DisableAdminAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<UserDto?> GetAdminAsync(string userId)
    {
        var admin = await mongoDbService.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        return admin.ToDto(aiQuotaService);
    }

    public Task GetAllSubscriptionsAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<PagedResult<UserDto?>> GetUsersAsync
    (int pageNumber,
    int pageSize,
    string? status,
    string? subscriptionType,
    string? search
    )
    {
        try
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);
            var skip = (pageNumber - 1) * pageSize;
            var sort = Builders<User>.Sort.Descending(u => u.CreatedAt);
            var filters = new List<FilterDefinition<User>>();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.GetNames(typeof(AccountStatus))
                        .Any(e => e.Equals(status, StringComparison.OrdinalIgnoreCase)))
                {
                    var statusEnum = (AccountStatus)Enum.Parse(typeof(AccountStatus), status, true);
                    filters.Add(Builders<User>.Filter.Eq(u => u.Status, statusEnum));
                }
                else
                {
                    return new PagedResult<UserDto?>
                    {
                        Items = new List<UserDto?>(),
                        TotalItems = 0,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(subscriptionType))
            {
                if (Enum.GetNames(typeof(SubscriptionType))
                        .Any(e => e.Equals(subscriptionType, StringComparison.OrdinalIgnoreCase)))
                {
                    var subscriptionTypeEnum = (SubscriptionType)Enum.Parse(typeof(SubscriptionType), subscriptionType, true);
                    filters.Add(Builders<User>.Filter.Eq(u => u.SubscriptionType, subscriptionTypeEnum));
                }
                else
                {
                    return new PagedResult<UserDto?>
                    {
                        Items = new List<UserDto?>(),
                        TotalItems = 0,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchRegex = new BsonRegularExpression(search, "i");
                var searchFilter = Builders<User>.Filter.Or(
                    Builders<User>.Filter.Regex(u => u.FirstName, searchRegex),
                    Builders<User>.Filter.Regex(u => u.LastName, searchRegex),
                    Builders<User>.Filter.Regex(u => u.Email, searchRegex)
                );

                filters.Add(searchFilter);
            }

            var filter = filters.Count > 0
                ? Builders<User>.Filter.And(filters)
                : Builders<User>.Filter.Empty;

            var countTask = mongoDbService.Users.CountDocumentsAsync(filter);
            var itemsTask = mongoDbService.Users
                .Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();

            await Task.WhenAll(countTask, itemsTask);

            var dtos = itemsTask.Result.Select(u => u.ToDto(aiQuotaService)).ToList();

            return new PagedResult<UserDto?>
            {
                Items = dtos,
                TotalItems = countTask.Result,
                PageNumber = pageNumber,
                PageSize = pageSize
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
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

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
            {
                expirationDate = DateTime.UtcNow.AddMonths(1);
            }
            else if (plan.SubscriptionType is SubscriptionType.PremiumYearly)
            {
                expirationDate = DateTime.UtcNow.AddYears(1);
            }
        }
        else
        {
            if (plan.SubscriptionType is SubscriptionType.Free or SubscriptionType.Premium)
            {
                expirationDate = null;
            }
        }

        var update = Builders<User>.Update
       .Set(x => x.SubscriptionType, plan.SubscriptionType)
       .Set(x => x.SubscriptionExpiresAt, expirationDate)
       .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<User>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updatedUser = await mongoDbService.Users.FindOneAndUpdateAsync(
            Builders<User>.Filter.Eq(x => x.Id, userId),
            update,
            options
        );

        if (updatedUser == null)
        {
            logger.LogError("Failed to update subscription for user {UserId}", userId);
            throw new InvalidOperationException($"User with ID {userId} not found or could not be updated");
        }

        logger.LogInformation(
            "Successfully updated subscription for user {UserId}: {SubscriptionType}",
            userId,
            updatedUser.SubscriptionType);

        return updatedUser.ToDto(aiQuotaService);
    }

    public async Task<DashboardAnalyticsDto> GetDashboardAnalyticsAsync()
    {
        var cacheKey = "dashboard_analytics";

        if (memoryCache.TryGetValue(cacheKey, out DashboardAnalyticsDto? cachedResult)
        && cachedResult != null)
        {
            return cachedResult;
        }

        var usersCollection = mongoDbService.Users;
        var now = DateTime.UtcNow;
        var firstDayOfThisMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfLastMonth = firstDayOfThisMonth.AddMonths(-1);
        var lastDayOfLastMonth = firstDayOfThisMonth.AddDays(-1);

        var totalUsersTask = usersCollection.CountDocumentsAsync(FilterDefinition<User>.Empty);
        var totalPremiumUsersTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.In(u => u.SubscriptionType, new[]
            {
                SubscriptionType.Premium,
                SubscriptionType.PremiumMonthly,
                SubscriptionType.PremiumYearly
            }));
        var totalFreeUsersTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.Free));
        var activeUsersTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.Eq(u => u.Status, AccountStatus.Active));

        var premiumMonthlyUsersTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.PremiumMonthly));
        var premiumYearlyUsersTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.PremiumYearly));
        var legacyPremiumUsersTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.Eq(u => u.SubscriptionType, SubscriptionType.Premium));

        var newUsersThisMonthTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.Gte(u => u.CreatedAt, firstDayOfThisMonth));

        var newUsersLastMonthTask = usersCollection.CountDocumentsAsync(
            Builders<User>.Filter.And(
                Builders<User>.Filter.Gte(u => u.CreatedAt, firstDayOfLastMonth),
                Builders<User>.Filter.Lte(u => u.CreatedAt, lastDayOfLastMonth)
            ));

        await Task.WhenAll(
            totalUsersTask,
            totalPremiumUsersTask,
            totalFreeUsersTask,
            activeUsersTask,
            newUsersThisMonthTask,
            newUsersLastMonthTask,
            premiumMonthlyUsersTask,
            premiumYearlyUsersTask,
            legacyPremiumUsersTask
        );

        var totalUsers = totalUsersTask.Result;
        var totalPremiumUsers = totalPremiumUsersTask.Result;
        var totalFreeUsers = totalFreeUsersTask.Result;
        var activeUsers = activeUsersTask.Result;
        var newUsersThisMonth = newUsersThisMonthTask.Result;
        var premiumMonthlyUsers = premiumMonthlyUsersTask.Result;
        var premiumYearlyUsers = premiumYearlyUsersTask.Result;
        var legacyPremiumUsers = legacyPremiumUsersTask.Result;

        var analytics = new DashboardAnalyticsDto(
            TotalUsers: totalUsers,
            TotalPremiumUsers: totalPremiumUsers,
            TotalFreeUsers: totalFreeUsers,
            ActiveUsers: activeUsers,
            NewUsersThisMonth: newUsersThisMonth,
            PremiumMonthlyUsers: premiumMonthlyUsers,
            PremiumYearlyUsers: premiumYearlyUsers,
            LegacyPremiumUsers: legacyPremiumUsers
        );

        var cacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromMinutes(5));

        memoryCache.Set(cacheKey, analytics, cacheEntryOptions);

        return analytics;
    }

    public async Task<DashboardStatisticsExportDto> GetDashboardStatisticsExportAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "dashboard_statistics_export_v1";
        if (memoryCache.TryGetValue(cacheKey, out DashboardStatisticsExportDto? cached) && cached != null)
            return cached;

        var users = mongoDbService.Users;
        var notes = mongoDbService.Notes;
        var verses = mongoDbService.SavedVerses;
        var activities = mongoDbService.RecentActivities;

        var now = DateTime.UtcNow;
        var firstDayOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstDayOfLastMonth = firstDayOfThisMonth.AddMonths(-1);
        var thirtyDaysAgo = now.Date.AddDays(-29);
        var sevenDaysAgo = now.AddDays(-7);
        var activitySince = now.AddDays(-30);
        var monthKey = AiQuotaService.GetUtcMonthKey(now);
        var freeLimit = aiQuotaService.FreeCallsPerMonth;

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

        var trackedMonthFilter = Builders<User>.Filter.Eq(u => u.AiFreeCallsMonthKey, monthKey);
        var usersTrackedMonthTask = users.CountDocumentsAsync(trackedMonthFilter, cancellationToken: cancellationToken);
        var usersAtLimitTask = users.CountDocumentsAsync(
            Builders<User>.Filter.And(
                trackedMonthFilter,
                Builders<User>.Filter.Gte(u => u.AiFreeCallsUsedInMonth, freeLimit)), cancellationToken: cancellationToken);

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

        var totalUsers = totalUsersTask.Result;
        var totalPremium = totalPremiumTask.Result;
        var newThisMonth = newThisMonthTask.Result;
        var newPrevMonth = newPrevMonthTask.Result;

        double? mom = newPrevMonth > 0
            ? (newThisMonth - newPrevMonth) / (double)newPrevMonth * 100.0
            : null;

        var createdInWindow = signupsCursorTask.Result;
        var byDayDict = createdInWindow
            .Select(dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).Date)
            .GroupBy(d => d)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var signupsSeries = new List<DailySignupCountDto>();
        for (var d = thirtyDaysAgo; d <= now.Date; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd");
            signupsSeries.Add(new DailySignupCountDto(key, byDayDict.GetValueOrDefault(d, 0)));
        }

        var export = new DashboardStatisticsExportDto
        {
            GeneratedAtUtc = now,
            Overview = new DashboardOverviewStatsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsersTask.Result,
                SuspendedUsers = suspendedUsersTask.Result,
                EmailVerifiedUsers = emailVerifiedTask.Result,
                EmailNotVerifiedUsers = emailNotVerifiedTask.Result
            },
            Subscriptions = new DashboardSubscriptionBreakdownDto
            {
                TotalPremiumUsers = totalPremium,
                TotalFreeUsers = totalFreeTask.Result,
                PremiumMonthlyUsers = premiumMonthlyTask.Result,
                PremiumYearlyUsers = premiumYearlyTask.Result,
                LegacyPremiumUsers = legacyPremiumTask.Result,
                PremiumPercentageOfTotal = totalUsers > 0 ? totalPremium / (double)totalUsers * 100.0 : 0,
                FreePercentageOfTotal = totalUsers > 0 ? totalFreeTask.Result / (double)totalUsers * 100.0 : 0
            },
            Growth = new DashboardGrowthStatsDto
            {
                NewUsersThisUtcMonth = newThisMonth,
                NewUsersPreviousUtcMonth = newPrevMonth,
                NewUsersLast7Days = newLast7DaysTask.Result,
                MonthOverMonthNewUserPercentChange = mom
            },
            Content = new DashboardContentStatsDto
            {
                TotalNotes = totalNotesTask.Result,
                TotalSavedVerses = totalVersesTask.Result
            },
            AiFreeTier = new DashboardAiFreeTierStatsDto
            {
                CurrentUtcMonthKey = monthKey,
                FreeCallsLimitPerMonth = freeLimit,
                UsersWithUsageTrackedThisMonth = usersTrackedMonthTask.Result,
                TotalFreeAiCallsUsedThisMonth = usageSumTask.Result.Sum(),
                UsersAtOrOverFreeLimitThisMonth = usersAtLimitTask.Result
            },
            Activity = new DashboardActivityStatsDto
            {
                ActivitiesLast30Days = activitiesTotalTask.Result,
                AiAnalysisActivitiesLast30Days = actAiTask.Result,
                AddNoteActivitiesLast30Days = actNoteTask.Result,
                ReadBibleActivitiesLast30Days = actReadTask.Result
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
            throw new InvalidOperationException($"User with ID {userId} not found");

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

        var update = Builders<User>.Update
            .Set(u => u.AiFreeCallsMonthKey, monthKey)
            .Set(u => u.AiFreeCallsUsedInMonth, 0)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<User>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updatedUser = await mongoDbService.Users.FindOneAndUpdateAsync(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            update,
            options,
            cancellationToken);

        if (updatedUser == null)
            throw new InvalidOperationException($"User with ID {userId} not found");

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

        var update = Builders<User>.Update
            .Set(u => u.AiFreeCallsMonthKey, monthKey)
            .Set(u => u.AiFreeCallsUsedInMonth, used)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<User>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updatedUser = await mongoDbService.Users.FindOneAndUpdateAsync(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            update,
            options,
            cancellationToken);

        if (updatedUser == null)
            throw new InvalidOperationException($"User with ID {userId} not found");

        return new AdminUserAiQuotaDto
        {
            UserId = userId,
            StoredMonthKey = updatedUser.AiFreeCallsMonthKey,
            StoredUsedInMonth = updatedUser.AiFreeCallsUsedInMonth,
            AiUsage = aiQuotaService.BuildUsageSnapshot(updatedUser)
        };
    }
}

