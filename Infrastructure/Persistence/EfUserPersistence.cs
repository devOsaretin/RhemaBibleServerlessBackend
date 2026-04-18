using System.Data;
using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Enums;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfUserPersistence(RhemaDbContext db) : IUserPersistence
{
  public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
    await db.Users.AsNoTracking().ToListAsync(cancellationToken);

  public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
    await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

  public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
    await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

  public async Task InsertAsync(User user, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrEmpty(user.Id))
      user.Id = Guid.NewGuid().ToString("N");
    var now = DateTime.UtcNow;
    if (user.CreatedAt == default)
      user.CreatedAt = now;
    if (user.UpdatedAt == default)
      user.UpdatedAt = now;
    db.Users.Add(user);
    await db.SaveChangesAsync(cancellationToken);
  }

  public async Task ReplaceAsync(string id, User user, CancellationToken cancellationToken = default)
  {
    user.Id = id;
    user.UpdatedAt = DateTime.UtcNow;
    db.Users.Update(user);
    await db.SaveChangesAsync(cancellationToken);
  }

  public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
  {
    await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken);
  }

  public async Task<User?> GetByRefreshTokenAsync(string refreshToken, DateTime utcNow, CancellationToken cancellationToken = default) =>
    await db.Users.AsNoTracking().FirstOrDefaultAsync(
      u => u.RefreshToken == refreshToken && u.RefreshTokenExpiryTime > utcNow,
      cancellationToken);

  public async Task UpdateRefreshTokenAsync(string userId, string refreshToken, DateTime refreshTokenExpiry, CancellationToken cancellationToken = default) =>
    await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(setters => setters
        .SetProperty(u => u.RefreshToken, refreshToken)
        .SetProperty(u => u.RefreshTokenExpiryTime, refreshTokenExpiry)
        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow),
      cancellationToken);

  public async Task<bool> UpdatePasswordAsync(string userId, string hashedPassword, CancellationToken cancellationToken = default)
  {

    var rowsAffected = await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(setters => setters
        .SetProperty(u => u.Password, hashedPassword)
        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow),
      cancellationToken);

    return rowsAffected > 0;
  }

  public async Task<bool> SetEmailVerifiedAsync(string email, bool verified, CancellationToken cancellationToken = default)
  {

    var rowsAffected = await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.IsEmailVerified, verified)
            .SetProperty(u => u.UpdatedAt, DateTime.UtcNow),
          cancellationToken);
    return rowsAffected > 0;
  }


  public async Task<User?> UpdateAccountStatusAsync(string userId, AccountStatus status, CancellationToken cancellationToken = default)
  {
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    if (user == null)
      return null;
    user.Status = status;
    user.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
    return user;
  }

  public async Task<PagedResult<User>> SearchAdminUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken = default)
  {
    var pageNumber = Math.Max(1, query.PageNumber);
    var pageSize = Math.Max(1, query.PageSize);
    var skip = (pageNumber - 1) * pageSize;

    var q = db.Users.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(query.Status))
    {
      if (!Enum.TryParse<AccountStatus>(query.Status, true, out var statusEnum))
      {
        return new PagedResult<User>
        {
          Items = [],
          TotalItems = 0,
          PageNumber = pageNumber,
          PageSize = pageSize
        };
      }

      q = q.Where(u => u.Status == statusEnum);
    }

    if (!string.IsNullOrWhiteSpace(query.SubscriptionType))
    {
      if (!Enum.TryParse<SubscriptionType>(query.SubscriptionType, true, out var subEnum))
      {
        return new PagedResult<User>
        {
          Items = [],
          TotalItems = 0,
          PageNumber = pageNumber,
          PageSize = pageSize
        };
      }

      q = q.Where(u => u.SubscriptionType == subEnum);
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = SanitizeIlikeTerm(query.Search);
      if (term.Length > 0)
      {
        var pattern = $"%{term}%";
        q = q.Where(u =>
          (u.FirstName != null && EF.Functions.ILike(u.FirstName, pattern)) ||
          (u.LastName != null && EF.Functions.ILike(u.LastName, pattern)) ||
          EF.Functions.ILike(u.Email, pattern));
      }
    }

    var total = await q.LongCountAsync(cancellationToken);
    var items = await q.OrderByDescending(u => u.CreatedAt).Skip(skip).Take(pageSize).ToListAsync(cancellationToken);

    return new PagedResult<User>
    {
      Items = items,
      TotalItems = total,
      PageNumber = pageNumber,
      PageSize = pageSize
    };
  }

  public async Task<User?> UpdateSubscriptionPlanAsync(
    string userId,
    SubscriptionType subscriptionType,
    DateTime? subscriptionExpiresAt,
    CancellationToken cancellationToken = default)
  {
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    if (user == null)
      return null;
    user.SubscriptionType = subscriptionType;
    user.SubscriptionExpiresAt = subscriptionExpiresAt;
    user.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
    return user;
  }

  public async Task<IReadOnlyList<User>> FindExpiredPremiumSubscriptionsAsync(DateTime now, CancellationToken cancellationToken = default)
  {
    var types = new[] { SubscriptionType.PremiumMonthly, SubscriptionType.PremiumYearly };
    return await db.Users.AsNoTracking()
      .Where(u => types.Contains(u.SubscriptionType) && u.SubscriptionExpiresAt != null && u.SubscriptionExpiresAt <= now)
      .ToListAsync(cancellationToken);
  }

  public async Task<bool> TryExpirePremiumSubscriptionAsync(string userId, DateTime now, CancellationToken cancellationToken = default)
  {
    var n = await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(setters => setters
        .SetProperty(u => u.SubscriptionType, SubscriptionType.Free)
        .SetProperty(u => u.SubscriptionExpiresAt, (DateTime?)null)
        .SetProperty(u => u.UpdatedAt, now),
      cancellationToken);
    return n > 0;
  }

  public Task<AiFreeQuotaConsumeResult?> TryConsumeFreeAiCallAsync(
    string userId,
    int monthlyLimit,
    string monthKey,
    CancellationToken cancellationToken = default)
  {
    var strategy = db.Database.CreateExecutionStrategy();
    return strategy.ExecuteAsync(async ct =>
    {
      await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
      try
      {
        var user = await db.Users
          .FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
          .AsTracking()
          .SingleOrDefaultAsync(ct);

        if (user == null)
        {
          await tx.RollbackAsync(ct);
          return null;
        }

        if (user.AiFreeCallsMonthKey == monthKey && user.AiFreeCallsUsedInMonth < monthlyLimit)
        {
          user.AiFreeCallsUsedInMonth++;
          await db.SaveChangesAsync(ct);
          await tx.CommitAsync(ct);
          return new AiFreeQuotaConsumeResult(
            FreeCallsRemainingThisMonth: Math.Max(0, monthlyLimit - user.AiFreeCallsUsedInMonth),
            MonthKeyUtc: monthKey,
            FreeCallsUsedThisMonth: user.AiFreeCallsUsedInMonth);
        }

        if (user.AiFreeCallsMonthKey != monthKey)
        {
          user.AiFreeCallsMonthKey = monthKey;
          user.AiFreeCallsUsedInMonth = 1;
          await db.SaveChangesAsync(ct);
          await tx.CommitAsync(ct);
          return new AiFreeQuotaConsumeResult(
            FreeCallsRemainingThisMonth: monthlyLimit - user.AiFreeCallsUsedInMonth,
            MonthKeyUtc: monthKey,
            FreeCallsUsedThisMonth: user.AiFreeCallsUsedInMonth);
        }

        await tx.RollbackAsync(ct);
        return null;
      }
      catch
      {
        await tx.RollbackAsync(ct);
        throw;
      }
    }, cancellationToken);
  }

  public async Task<User?> ResetAiQuotaForCurrentMonthAsync(string userId, string monthKey, CancellationToken cancellationToken = default)
  {
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    if (user == null)
      return null;
    user.AiFreeCallsMonthKey = monthKey;
    user.AiFreeCallsUsedInMonth = 0;
    user.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
    return user;
  }

  public async Task<User?> SetAiQuotaUsedForMonthAsync(string userId, string monthKey, int used, CancellationToken cancellationToken = default)
  {
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    if (user == null)
      return null;
    user.AiFreeCallsMonthKey = monthKey;
    user.AiFreeCallsUsedInMonth = used;
    user.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
    return user;
  }

  private static string SanitizeIlikeTerm(string input) =>
    new string(input.Trim().Where(c => c is not '%' and not '_').ToArray());
}
