using MongoDB.Bson;
using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class MongoUserPersistence(IMongoDbService mongo) : IUserPersistence
{
  public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
    mongo.Users.Find(_ => true).ToListAsync(cancellationToken);

  public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
  {
    return await mongo.Users.Find(u => u.Id == id).FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
  {
    return await mongo.Users.Find(u => u.Email == email).FirstOrDefaultAsync(cancellationToken);
  }

  public Task InsertAsync(User user, CancellationToken cancellationToken = default) =>
    mongo.Users.InsertOneAsync(user, new InsertOneOptions(), cancellationToken);

  public Task ReplaceAsync(string id, User user, CancellationToken cancellationToken = default) =>
    mongo.Users.ReplaceOneAsync(u => u.Id == id, user, cancellationToken: cancellationToken);

  public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
    mongo.Users.DeleteOneAsync(u => u.Id == id, cancellationToken);

  public async Task<User?> GetByRefreshTokenAsync(string refreshToken, DateTime utcNow, CancellationToken cancellationToken = default)
  {
    var filter = Builders<User>.Filter.And(
      Builders<User>.Filter.Eq(u => u.RefreshToken, refreshToken),
      Builders<User>.Filter.Gt(u => u.RefreshTokenExpiryTime, utcNow));
    return await mongo.Users.Find(filter).FirstOrDefaultAsync(cancellationToken);
  }

  public Task UpdateRefreshTokenAsync(string userId, string refreshToken, DateTime refreshTokenExpiry, CancellationToken cancellationToken = default)
  {
    var update = Builders<User>.Update
      .Set(u => u.RefreshToken, refreshToken)
      .Set(u => u.RefreshTokenExpiryTime, refreshTokenExpiry)
      .Set(u => u.UpdatedAt, DateTime.UtcNow);
    return mongo.Users.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: cancellationToken);
  }

  public Task UpdatePasswordAsync(string userId, string hashedPassword, CancellationToken cancellationToken = default)
  {
    var update = Builders<User>.Update
      .Set(u => u.Password, hashedPassword)
      .Set(u => u.UpdatedAt, DateTime.UtcNow);
    return mongo.Users.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: cancellationToken);
  }

  public Task SetEmailVerifiedAsync(string userId, bool verified, CancellationToken cancellationToken = default)
  {
    var update = Builders<User>.Update
      .Set(u => u.IsEmailVerified, verified)
      .Set(u => u.UpdatedAt, DateTime.UtcNow);
    return mongo.Users.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: cancellationToken);
  }

  public async Task<User?> UpdateAccountStatusAsync(string userId, AccountStatus status, CancellationToken cancellationToken = default)
  {
    var update = Builders<User>.Update
      .Set(x => x.Status, status)
      .Set(x => x.UpdatedAt, DateTime.UtcNow);
    var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
    return await mongo.Users.FindOneAndUpdateAsync(
      Builders<User>.Filter.Eq(x => x.Id, userId),
      update,
      options,
      cancellationToken);
  }

  public async Task<PagedResult<User>> SearchAdminUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken = default)
  {
    var pageNumber = Math.Max(1, query.PageNumber);
    var pageSize = Math.Max(1, query.PageSize);
    var skip = (pageNumber - 1) * pageSize;
    var sort = Builders<User>.Sort.Descending(u => u.CreatedAt);
    var filters = new List<FilterDefinition<User>>();

    if (!string.IsNullOrWhiteSpace(query.Status))
    {
      if (!Enum.GetNames(typeof(AccountStatus)).Any(e => e.Equals(query.Status, StringComparison.OrdinalIgnoreCase)))
      {
        return new PagedResult<User>
        {
          Items = [],
          TotalItems = 0,
          PageNumber = pageNumber,
          PageSize = pageSize
        };
      }

      var statusEnum = (AccountStatus)Enum.Parse(typeof(AccountStatus), query.Status, true);
      filters.Add(Builders<User>.Filter.Eq(u => u.Status, statusEnum));
    }

    if (!string.IsNullOrWhiteSpace(query.SubscriptionType))
    {
      if (!Enum.GetNames(typeof(SubscriptionType)).Any(e => e.Equals(query.SubscriptionType, StringComparison.OrdinalIgnoreCase)))
      {
        return new PagedResult<User>
        {
          Items = [],
          TotalItems = 0,
          PageNumber = pageNumber,
          PageSize = pageSize
        };
      }

      var subscriptionTypeEnum = (SubscriptionType)Enum.Parse(typeof(SubscriptionType), query.SubscriptionType, true);
      filters.Add(Builders<User>.Filter.Eq(u => u.SubscriptionType, subscriptionTypeEnum));
    }

    if (!string.IsNullOrEmpty(query.Search))
    {
      var searchRegex = new BsonRegularExpression(query.Search, "i");
      var searchFilter = Builders<User>.Filter.Or(
        Builders<User>.Filter.Regex(u => u.FirstName, searchRegex),
        Builders<User>.Filter.Regex(u => u.LastName, searchRegex),
        Builders<User>.Filter.Regex(u => u.Email, searchRegex));
      filters.Add(searchFilter);
    }

    var filter = filters.Count > 0
      ? Builders<User>.Filter.And(filters)
      : Builders<User>.Filter.Empty;

    var countTask = mongo.Users.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    var itemsTask = mongo.Users.Find(filter).Sort(sort).Skip(skip).Limit(pageSize).ToListAsync(cancellationToken);
    await Task.WhenAll(countTask, itemsTask);

    return new PagedResult<User>
    {
      Items = itemsTask.Result,
      TotalItems = countTask.Result,
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
    var update = Builders<User>.Update
      .Set(x => x.SubscriptionType, subscriptionType)
      .Set(x => x.SubscriptionExpiresAt, subscriptionExpiresAt)
      .Set(x => x.UpdatedAt, DateTime.UtcNow);
    var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
    return await mongo.Users.FindOneAndUpdateAsync(
      Builders<User>.Filter.Eq(x => x.Id, userId),
      update,
      options,
      cancellationToken);
  }

  public async Task<IReadOnlyList<User>> FindExpiredPremiumSubscriptionsAsync(DateTime now, CancellationToken cancellationToken = default)
  {
    var filter = Builders<User>.Filter.And(
      Builders<User>.Filter.In(u => u.SubscriptionType, new[]
      {
        SubscriptionType.PremiumMonthly,
        SubscriptionType.PremiumYearly
      }),
      Builders<User>.Filter.Lte(u => u.SubscriptionExpiresAt, now),
      Builders<User>.Filter.Ne(u => u.SubscriptionExpiresAt, null));
    var list = await mongo.Users.Find(filter).ToListAsync(cancellationToken);
    return list;
  }

  public async Task<bool> TryExpirePremiumSubscriptionAsync(string userId, DateTime now, CancellationToken cancellationToken = default)
  {
    var update = Builders<User>.Update
      .Set(u => u.SubscriptionType, SubscriptionType.Free)
      .Set(u => u.SubscriptionExpiresAt, (DateTime?)null)
      .Set(u => u.UpdatedAt, now);
    var result = await mongo.Users.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: cancellationToken);
    return result.ModifiedCount > 0;
  }

  public async Task<AiFreeQuotaConsumeResult?> TryConsumeFreeAiCallAsync(
    string userId,
    int monthlyLimit,
    string monthKey,
    CancellationToken cancellationToken = default)
  {
    var filterSameMonthHasQuota =
      Builders<User>.Filter.Eq(u => u.Id, userId) &
      Builders<User>.Filter.Eq(u => u.AiFreeCallsMonthKey, monthKey) &
      Builders<User>.Filter.Lt(u => u.AiFreeCallsUsedInMonth, monthlyLimit);

    var updateInc = Builders<User>.Update.Inc(u => u.AiFreeCallsUsedInMonth, 1);

    var updated = await mongo.Users.FindOneAndUpdateAsync(
      filterSameMonthHasQuota,
      updateInc,
      new FindOneAndUpdateOptions<User> { IsUpsert = false, ReturnDocument = ReturnDocument.After },
      cancellationToken);

    if (updated != null)
    {
      return new AiFreeQuotaConsumeResult(
        FreeCallsRemainingThisMonth: Math.Max(0, monthlyLimit - updated.AiFreeCallsUsedInMonth),
        MonthKeyUtc: monthKey,
        FreeCallsUsedThisMonth: updated.AiFreeCallsUsedInMonth);
    }

    var filterNewMonth =
      Builders<User>.Filter.Eq(u => u.Id, userId) &
      Builders<User>.Filter.Ne(u => u.AiFreeCallsMonthKey, monthKey);

    var updateResetToOne = Builders<User>.Update
      .Set(u => u.AiFreeCallsMonthKey, monthKey)
      .Set(u => u.AiFreeCallsUsedInMonth, 1);

    updated = await mongo.Users.FindOneAndUpdateAsync(
      filterNewMonth,
      updateResetToOne,
      new FindOneAndUpdateOptions<User> { IsUpsert = false, ReturnDocument = ReturnDocument.After },
      cancellationToken);

    if (updated != null)
    {
      return new AiFreeQuotaConsumeResult(
        FreeCallsRemainingThisMonth: monthlyLimit - updated.AiFreeCallsUsedInMonth,
        MonthKeyUtc: monthKey,
        FreeCallsUsedThisMonth: updated.AiFreeCallsUsedInMonth);
    }

    return null;
  }

  public async Task<User?> ResetAiQuotaForCurrentMonthAsync(string userId, string monthKey, CancellationToken cancellationToken = default)
  {
    var update = Builders<User>.Update
      .Set(u => u.AiFreeCallsMonthKey, monthKey)
      .Set(u => u.AiFreeCallsUsedInMonth, 0)
      .Set(u => u.UpdatedAt, DateTime.UtcNow);
    var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
    return await mongo.Users.FindOneAndUpdateAsync(
      Builders<User>.Filter.Eq(u => u.Id, userId),
      update,
      options,
      cancellationToken);
  }

  public async Task<User?> SetAiQuotaUsedForMonthAsync(string userId, string monthKey, int used, CancellationToken cancellationToken = default)
  {
    var update = Builders<User>.Update
      .Set(u => u.AiFreeCallsMonthKey, monthKey)
      .Set(u => u.AiFreeCallsUsedInMonth, used)
      .Set(u => u.UpdatedAt, DateTime.UtcNow);
    var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };
    return await mongo.Users.FindOneAndUpdateAsync(
      Builders<User>.Filter.Eq(u => u.Id, userId),
      update,
      options,
      cancellationToken);
  }
}
