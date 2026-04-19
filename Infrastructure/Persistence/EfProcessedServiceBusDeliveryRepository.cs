using Microsoft.EntityFrameworkCore;
using Npgsql;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfProcessedServiceBusDeliveryRepository(RhemaDbContext db) : IProcessedServiceBusDeliveryRepository
{
  public async Task<bool> TryInsertProcessedDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default)
  {
    db.ProcessedServiceBusDeliveries.Add(new ProcessedServiceBusDelivery
    {
      Id = deliveryId,
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
