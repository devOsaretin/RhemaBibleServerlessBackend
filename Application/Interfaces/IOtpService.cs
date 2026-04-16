


public interface IOtpService
{
    Task<string> GenerateOtpAsync(string userId, OtpType type, string? email = null, CancellationToken cancellationToken = default);
    Task<bool> VerifyOtpAsync(string email, string code, OtpType type, CancellationToken cancellationToken = default);
    Task<bool> InvalidateOtpAsync(string userId, string code, OtpType type, CancellationToken cancellationToken = default);
}
