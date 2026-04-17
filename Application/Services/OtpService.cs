using RhemaBibleAppServerless.Application.Persistence;

public class OtpService(
  IOtpRepository otpRepository,
  IConfiguration configuration,
  INotificationService notificationService) : IOtpService
{
  private Task IncrementAttempts(OtpCode otp, CancellationToken cancellationToken) =>
    otpRepository.IncrementAttemptsAsync(otp.Id!, cancellationToken);

  public async Task<string> GenerateOtpAsync(string userId, OtpType type, string? email = null, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(email))
      throw new ArgumentException("Email is required for generating OTP");
    var otp = new Random().Next(100000, 999999).ToString();

    await otpRepository.InvalidateActiveOtpsAsync(email!, type, cancellationToken);

    var lifetimeMinutes = configuration.GetValue<int>("Otp:LifetimeMinutes", 10);

    var otpCode = new OtpCode
    {
      Code = otp,
      Email = email!,
      Type = type,
      ExpiresAt = DateTime.UtcNow.AddMinutes(lifetimeMinutes),
      Attempts = 0,
      IsUsed = false,
      UserId = userId
    };

    await otpRepository.InsertAsync(otpCode, cancellationToken);

    var subject = "Rhema Bible — Your verification code";
    var body = EmailTemplates.VerificationCode(otp, lifetimeMinutes);

    await notificationService.SendAsync(email!, subject, body, cancellationToken);

    return otp;
  }

  public Task<bool> InvalidateOtpAsync(string userId, string code, OtpType type, CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  public async Task<bool> VerifyOtpAsync(string email, string code, OtpType type, CancellationToken cancellationToken = default)
  {
    var otp = await otpRepository.FindByCodeAndTypeAsync(code, type, cancellationToken);

    if (otp == null)
      return false;

    if (otp.Email != email)
      return false;

    if (otp.Attempts >= 5)
      return false;

    if (!otp.IsValid)
    {
      await IncrementAttempts(otp, cancellationToken);
      return false;
    }

    await otpRepository.MarkUsedAsync(otp.Id!, cancellationToken);

    return true;
  }
}
