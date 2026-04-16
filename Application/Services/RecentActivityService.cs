using Microsoft.Extensions.Caching.Memory;
using RhemaBibleAppServerless.Application.Persistence;

public class RecentActivityService(
  IRecentActivityRepository activities,
  IMemoryCache memoryCache,
  IUserResourceEpochStore epochStore) : IRecentActivityService
{
  public async Task<RecentActivity> AddActivityByUser(RecentActivity activity)
  {
    await activities.InsertAsync(activity, CancellationToken.None);
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
      return await activities.GetRecentByUserAsync(userId, 3, CancellationToken.None);
    }))!;
  }
}
