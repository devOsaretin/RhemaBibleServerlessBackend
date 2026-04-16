

public interface IAdminService

{
    Task AdminLoginAsync();
    Task DisableAdminAsync();

    Task CreateAdminAsync();
    Task<UserDto?> GetAdminAsync(string userId);
    Task<PagedResult<UserDto?>> GetUsersAsync(
        int pageNumber,
        int pageSize,
        string? status,
        string? subscriptionType,
        string? search);

    Task<UserDto?> GetUserAsync(string userId, CancellationToken cancellationToken);

    Task<UserDto> DeactivateUserAsync(string userId);
    Task<UserDto> ActivateUserAsync(string userId);
    Task<UserDto> UpdateUsersPlanAsync(string userId, UpdateSubscriptionDto plan, CancellationToken cancellationToken);

    Task GetAllSubscriptionsAsync();
    Task<DashboardAnalyticsDto> GetDashboardAnalyticsAsync();
    Task<DashboardStatisticsExportDto> GetDashboardStatisticsExportAsync(CancellationToken cancellationToken = default);

    Task<AdminUserAiQuotaDto> GetUserAiQuotaAsync(string userId, CancellationToken cancellationToken = default);
    Task<AdminUserAiQuotaDto> ResetUserAiQuotaAsync(string userId, CancellationToken cancellationToken = default);
    Task<AdminUserAiQuotaDto> SetUserAiQuotaRemainingAsync(string userId, int remainingThisMonth, CancellationToken cancellationToken = default);
}