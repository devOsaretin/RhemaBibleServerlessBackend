using MongoDB.Driver;

public class OtpService(
IMongoDbService mongoDbService,
IConfiguration configuration,
INotificationService notificationService) : IOtpService
{

    private async Task IncrementAttempts(OtpCode otp, CancellationToken cancellationToken)
    {
        await mongoDbService.OtpCode.UpdateOneAsync(
            o => o.Id == otp.Id,
            Builders<OtpCode>.Update.Inc(o => o.Attempts, 1),
            cancellationToken: cancellationToken
        );
    }
    public async Task<string> GenerateOtpAsync(string userId, OtpType type, string? email = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required for generating OTP");
        var otp = new Random().Next(100000, 999999).ToString();

        // Invalidate previous OTPs for this user+type
        var filter = Builders<OtpCode>.Filter.Eq(o => o.Email, email) &
                     Builders<OtpCode>.Filter.Eq(o => o.Type, type) &
                     Builders<OtpCode>.Filter.Eq(o => o.IsUsed, false);

        var update = Builders<OtpCode>.Update
            .Set(o => o.IsUsed, true)
            .Set(o => o.UsedAt, DateTime.UtcNow);


        await mongoDbService.OtpCode.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);

        // Create new OTP with expiration
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

        await mongoDbService.OtpCode.InsertOneAsync(otpCode, cancellationToken: cancellationToken);

        // Send OTP via email
        var subject = "Rhema Bible — Your verification code";
        var body = EmailTemplates.VerificationCode(otp, lifetimeMinutes);

        await notificationService.SendAsync(email!, subject, body, cancellationToken);

        return otp;
    }

    public Task<bool> InvalidateOtpAsync(string userId, string code, OtpType type, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> VerifyOtpAsync(string email, string code, OtpType type, CancellationToken cancellationToken = default)
    {
        var otp = await mongoDbService.OtpCode
            .Find(o => o.Code == code && o.Type == type)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp.Email != email) return false;

        if (otp == null)
            return false;

        // Increase attempts
        if (otp.Attempts >= 5)
            return false;

        if (!otp.IsValid)
        {
            await IncrementAttempts(otp, cancellationToken);
            return false;
        }

        // OTP Valid → Mark as used
        var update = Builders<OtpCode>.Update
            .Set(o => o.IsUsed, true)
            .Set(o => o.UsedAt, DateTime.UtcNow);

        await mongoDbService.OtpCode.UpdateOneAsync(
            o => o.Id == otp.Id,
            update,
            cancellationToken: cancellationToken);

        return true;
    }
}

