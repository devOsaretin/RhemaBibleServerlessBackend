using Microsoft.EntityFrameworkCore;
using Npgsql;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfProcessedWebhookRepository(RhemaDbContext db) : IProcessedWebhookRepository
{
  public async Task<bool> TryInsertProcessedEventAsync(string eventId, CancellationToken cancellationToken = default)
  {
    db.ProcessedWebhooks.Add(new ProcessedWebhook
    {
      Id = eventId,
      ProcessedAt = DateTime.UtcNow
    });
    try
    {
      await db.SaveChangesAsync(cancellationToken);
      return true;
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
      db.ChangeTracker.Clear();
      return false;
    }
  }
}
