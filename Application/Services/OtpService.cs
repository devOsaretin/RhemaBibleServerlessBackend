using System.Security.Cryptography;
using System.Text;
public class OtpService(
  IOtpRepository otpRepository,
  IConfiguration configuration,
  IServiceBusService serviceBusService,
  ILogger<OtpService> logger) : IOtpService
{
  private Task IncrementAttempts(OtpCode otp, CancellationToken cancellationToken) =>
    otpRepository.IncrementAttemptsAsync(otp.Id!, cancellationToken);

  public async Task<string> GenerateOtpAsync(string userId, OtpType type, string? email = null, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(email))
      throw new ArgumentException("Email is required for generating OTP");
    var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    await otpRepository.InvalidateActiveOtpsAsync(email!, type, cancellationToken);

    var lifetimeMinutes = configuration.GetValue<int>("Otp:LifetimeMinutes", 10);

    var otpCode = new OtpCode
    {
      Code = HashOtp(otp),
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

    var queueMessage = new EmailRequestFromQueueDto
    {
      Body = body,
      Subject = subject,
      Recipient = email
    };

    await serviceBusService.PublishAsync(queueMessage, QueueNames.Email, cancellationToken);
    logger.LogInformation("Message publish to queue: {queue}", QueueNames.Email);

    return otp;
  }

  public Task<bool> InvalidateOtpAsync(string userId, string code, OtpType type, CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  public async Task<bool> VerifyOtpAsync(string email, string code, OtpType type, CancellationToken cancellationToken = default)
  {
    var hashedCode = HashOtp(code);
    var otp = await otpRepository.FindByCodeAndTypeAsync(hashedCode, type, email, cancellationToken);

    if (otp == null)
      return false;

    if (otp.Email != email)
    {
      await IncrementAttempts(otp, cancellationToken);
      return false;
    }

    if (otp.Attempts >= 5)
      return false;

    if (!otp.IsValid)
    {
      await IncrementAttempts(otp, cancellationToken);
      return false;
    }

    var updated = await otpRepository.MarkUsedAsync(otp.Id!, cancellationToken);

    return updated;
  }

  private static string HashOtp(string otp)
  {
    using var sha = SHA256.Create();
    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(otp));
    return Convert.ToBase64String(bytes);
  }
}
