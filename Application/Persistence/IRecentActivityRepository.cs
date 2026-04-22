namespace RhemaBibleAppServerless.Application.Persistence;

public interface IRecentActivityRepository
{
  Task InsertAsync(RecentActivity activity, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<RecentActivity>> GetRecentByUserAsync(string userId, int take, CancellationToken cancellationToken = default);
  Task<int> DeleteAllByUserAsync(string userId, CancellationToken cancellationToken = default);
}
