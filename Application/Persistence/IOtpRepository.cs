namespace RhemaBibleAppServerless.Application.Persistence;

public interface IOtpRepository
{
  Task InvalidateActiveOtpsAsync(string email, OtpType type, CancellationToken cancellationToken = default);
  Task InsertAsync(OtpCode code, CancellationToken cancellationToken = default);
  Task<OtpCode?> FindByCodeAndTypeAsync(string code, OtpType type, string email, CancellationToken cancellationToken = default);
  Task IncrementAttemptsAsync(string otpId, CancellationToken cancellationToken = default);
  Task<bool> MarkUsedAsync(string otpId, CancellationToken cancellationToken = default);
  Task<int> DeleteAllByUserAsync(string userId, CancellationToken cancellationToken = default);
}
