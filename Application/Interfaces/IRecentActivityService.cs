


public interface IRecentActivityService
{
    Task<RecentActivity> AddActivityByUser(RecentActivity activity);
    Task<IReadOnlyList<RecentActivity>> GetRecentActivitiesByUserAsync(string userId);

}
