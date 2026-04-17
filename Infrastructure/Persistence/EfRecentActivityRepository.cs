using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfRecentActivityRepository(RhemaDbContext db) : IRecentActivityRepository
{
  public async Task InsertAsync(RecentActivity activity, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrEmpty(activity.Id))
      activity.Id = Guid.NewGuid().ToString("N");
    db.RecentActivities.Add(activity);
    await db.SaveChangesAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<RecentActivity>> GetRecentByUserAsync(string userId, int take, CancellationToken cancellationToken = default) =>
    await db.RecentActivities.AsNoTracking()
      .Where(a => a.AuthId == userId)
      .OrderByDescending(a => a.CreatedAt)
      .Take(take)
      .ToListAsync(cancellationToken);
}
