namespace RhemaBibleAppServerless.Application.Persistence;

/// <summary>Mongo-backed user document access. Implementations live in Infrastructure.</summary>
public interface IUserPersistence
{
  Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default);
  Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
  Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
  Task InsertAsync(User user, CancellationToken cancellationToken = default);
  Task ReplaceAsync(string id, User user, CancellationToken cancellationToken = default);
  Task DeleteAsync(string id, CancellationToken cancellationToken = default);

  Task<User?> GetByRefreshTokenAsync(string refreshToken, DateTime utcNow, CancellationToken cancellationToken = default);
  Task UpdateRefreshTokenAsync(string userId, string refreshToken, DateTime refreshTokenExpiry, CancellationToken cancellationToken = default);
  Task UpdatePasswordAsync(string userId, string hashedPassword, CancellationToken cancellationToken = default);
  Task SetEmailVerifiedAsync(string userId, bool verified, CancellationToken cancellationToken = default);

  Task<User?> UpdateAccountStatusAsync(string userId, AccountStatus status, CancellationToken cancellationToken = default);
  Task<PagedResult<User>> SearchAdminUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken = default);

  Task<User?> UpdateSubscriptionPlanAsync(
    string userId,
    SubscriptionType subscriptionType,
    DateTime? subscriptionExpiresAt,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<User>> FindExpiredPremiumSubscriptionsAsync(DateTime now, CancellationToken cancellationToken = default);
  Task<bool> TryExpirePremiumSubscriptionAsync(string userId, DateTime now, CancellationToken cancellationToken = default);

  Task<AiFreeQuotaConsumeResult?> TryConsumeFreeAiCallAsync(
    string userId,
    int monthlyLimit,
    string monthKey,
    CancellationToken cancellationToken = default);

  Task<User?> ResetAiQuotaForCurrentMonthAsync(string userId, string monthKey, CancellationToken cancellationToken = default);
  Task<User?> SetAiQuotaUsedForMonthAsync(string userId, string monthKey, int used, CancellationToken cancellationToken = default);
}
