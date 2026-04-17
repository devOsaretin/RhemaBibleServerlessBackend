using Microsoft.EntityFrameworkCore;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Models;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class EfOtpRepository(RhemaDbContext db) : IOtpRepository
{
  public Task InvalidateActiveOtpsAsync(string email, OtpType type, CancellationToken cancellationToken = default) =>
    db.OtpCodes.Where(o => o.Email == email && o.Type == type && !o.IsUsed).ExecuteUpdateAsync(setters => setters
        .SetProperty(o => o.IsUsed, true)
        .SetProperty(o => o.UsedAt, DateTime.UtcNow),
      cancellationToken);

  public async Task InsertAsync(OtpCode code, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrEmpty(code.Id))
      code.Id = Guid.NewGuid().ToString("N");
    db.OtpCodes.Add(code);
    await db.SaveChangesAsync(cancellationToken);
  }

  public Task<OtpCode?> FindByCodeAndTypeAsync(string code, OtpType type, CancellationToken cancellationToken = default) =>
    db.OtpCodes.AsNoTracking().FirstOrDefaultAsync(o => o.Code == code && o.Type == type, cancellationToken);

  public Task IncrementAttemptsAsync(string otpId, CancellationToken cancellationToken = default) =>
    db.OtpCodes.Where(o => o.Id == otpId).ExecuteUpdateAsync(
      setters => setters.SetProperty(o => o.Attempts, o => o.Attempts + 1),
      cancellationToken);

  public Task MarkUsedAsync(string otpId, CancellationToken cancellationToken = default) =>
    db.OtpCodes.Where(o => o.Id == otpId).ExecuteUpdateAsync(setters => setters
        .SetProperty(o => o.IsUsed, true)
        .SetProperty(o => o.UsedAt, DateTime.UtcNow),
      cancellationToken);
}
