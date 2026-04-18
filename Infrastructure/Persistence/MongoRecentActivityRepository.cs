using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class MongoRecentActivityRepository(IMongoDbService mongo) : IRecentActivityRepository
{
  public Task InsertAsync(RecentActivity activity, CancellationToken cancellationToken = default) =>
    mongo.RecentActivities.InsertOneAsync(activity, cancellationToken: cancellationToken);

  public async Task<IReadOnlyList<RecentActivity>> GetRecentByUserAsync(string userId, int take, CancellationToken cancellationToken = default)
  {
    var filter = Builders<RecentActivity>.Filter.Eq(a => a.AuthId, userId);
    var list = await mongo.RecentActivities
      .Find(filter)
      .SortByDescending(a => a.CreatedAt)
      .Limit(take)
      .ToListAsync(cancellationToken);
    return list;
  }
}
