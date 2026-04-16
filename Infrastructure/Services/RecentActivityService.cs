using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

public class RecentActivityService(
    IMongoDbService mongoDbService,
    IMemoryCache memoryCache,
    IUserResourceEpochStore epochStore) : IRecentActivityService
{
    public async Task<RecentActivity> AddActivityByUser(RecentActivity activity)
    {
        await mongoDbService.RecentActivities.InsertOneAsync(activity);
        if (!string.IsNullOrEmpty(activity.AuthId))
            epochStore.BumpRecentActivity(activity.AuthId);
        return activity;
    }

    public async Task<IReadOnlyList<RecentActivity>> GetRecentActivitiesByUserAsync(string userId)
    {
        var epoch = epochStore.GetRecentActivityEpoch(userId);
        var cacheKey = $"ra:v1:{userId}:{epoch}";

        return (await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45);
            var filter = Builders<RecentActivity>.Filter.Eq(a => a.AuthId, userId);
            var list = await mongoDbService.RecentActivities
                .Find(filter)
                .SortByDescending(a => a.CreatedAt)
                .Limit(3)
                .ToListAsync();
            return (IReadOnlyList<RecentActivity>)list;
        }))!;
    }
}

