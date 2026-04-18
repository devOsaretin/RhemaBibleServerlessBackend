using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Application.Persistence;

public class AuthService(
  IUserPersistence users,
  IUserApplicationService userService,
  IPasswordHasher passwordHasher,
  IJwtService jwtService,
  IOtpService otpService,
  INotificationService notificationService,
  IAiQuotaService aiQuotaService,
  ILogger<AuthService> logger,
  IConfiguration configuration) : IAuthService
{
  private readonly int _jwtExpirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes", 60);
  private readonly int _refreshTokenExpirationDays = configuration.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7);

  public async Task ForgotPasswordAsync(ForgotPasswordRequest forgotPasswordRequest, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByEmailAsync(forgotPasswordRequest.Email, cancellationToken);

    if (user == null)
      return;

    if (user.Id != null)
      await otpService.GenerateOtpAsync(user.Id, OtpType.PasswordReset, user.Email, cancellationToken);
  }

  public Task<string> GenerateTokenAsync(User user, string email, int? expirationMinutes = null)
  {
    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, user.Id!.ToString()),
      new(ClaimTypes.Email, email),
      new("jti", Guid.NewGuid().ToString()),
    };

    var jwtExpirationMinutes = expirationMinutes ?? _jwtExpirationMinutes;
    var token = jwtService.GenerateToken(claims, expirationMinutes: jwtExpirationMinutes);
    return Task.FromResult(token);
  }

  public async Task<UserDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByIdAsync(userId, cancellationToken)
      ?? throw new UserNotFoundException(userId);
    return user.ToDto(aiQuotaService);
  }

  public async Task<AuthResponse> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByEmailAsync(loginRequest.Email, cancellationToken);

    if (user != null && passwordHasher.VerifyPassword(loginRequest.Password, user.Password))
    {
      var token = await GenerateTokenAsync(user, user.Email, _jwtExpirationMinutes);
      var refreshToken = jwtService.GenerateRefreshToken();
      var refreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

      await users.UpdateRefreshTokenAsync(user.Id!, refreshToken, refreshTokenExpiry, cancellationToken);

      var userDto = user.ToDto(aiQuotaService, includeAiUsage: false);
      return new AuthResponse
      {
        User = userDto,
        AiUsage = aiQuotaService.BuildUsageSnapshot(user),
        Token = token,
        RefreshToken = refreshToken,
        ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtExpirationMinutes),
      };
    }

    throw new UnauthorizedAccessException("Invalid credentials");
  }

  public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
  {
    var user = await users.GetByRefreshTokenAsync(refreshToken, DateTime.UtcNow, cancellationToken);

    if (user == null)
      throw new UnauthorizedAccessException("Invalid or expired refresh token");

    var newToken = await GenerateTokenAsync(user, user.Email, expirationMinutes: _jwtExpirationMinutes);
    var newRefreshToken = jwtService.GenerateRefreshToken();
    var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

    await users.UpdateRefreshTokenAsync(user.Id!, newRefreshToken, newRefreshTokenExpiry, cancellationToken);

    var userDto = user.ToDto(aiQuotaService, includeAiUsage: false);
    return new AuthResponse
    {
      User = userDto,
      AiUsage = aiQuotaService.BuildUsageSnapshot(user),
      Token = newToken,
      RefreshToken = newRefreshToken,
      ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtExpirationMinutes),
    };
  }

  public async Task<AuthResponse> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByEmailAsync(registerRequest.Email, cancellationToken);

    if (user != null) throw new ConflictException("Email is already taken");

    var newUser = await userService.FindOrCreateAsync(registerRequest, cancellationToken);

    if (newUser.Id != null)
      await otpService.GenerateOtpAsync(newUser.Id, OtpType.EmailVerification, newUser.Email, cancellationToken);

    var token = await GenerateTokenAsync(newUser, registerRequest.Email, _jwtExpirationMinutes);
    var refreshToken = jwtService.GenerateRefreshToken();
    var refreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

    await users.UpdateRefreshTokenAsync(newUser.Id!, refreshToken, refreshTokenExpiry, cancellationToken);

    var newUserDto = newUser.ToDto(aiQuotaService, includeAiUsage: false);
    return new AuthResponse
    {
      User = newUserDto,
      AiUsage = aiQuotaService.BuildUsageSnapshot(newUser),
      Token = token,
      RefreshToken = refreshToken,
      ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtExpirationMinutes),
    };
  }

  public async Task ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByEmailAsync(resetPasswordRequest.Email, cancellationToken)
      ?? throw new ResourceNotFoundException($"User with email '{resetPasswordRequest.Email}' was not found.");

    var isValidOtp = await otpService.VerifyOtpAsync(
      resetPasswordRequest.Email,
      resetPasswordRequest.Otp,
      OtpType.PasswordReset,
      cancellationToken);

    if (!isValidOtp)
      throw new UnauthorizedAccessException("Invalid or expired OTP");

    var hashedPassword = passwordHasher.HashPassword(resetPasswordRequest.NewPassword);
    await users.UpdatePasswordAsync(user.Id!, hashedPassword, cancellationToken);

    userService.ClearCachedUser(user.Id!);
    NotifyPasswordChangedFireAndForget(user.Id, user.Email);
  }

  public async Task ChangePasswordAsync(string userId, ChangePasswordRequest changePasswordRequest, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByIdAsync(userId, cancellationToken)
      ?? throw new UserNotFoundException(userId);

    if (!passwordHasher.VerifyPassword(changePasswordRequest.OldPassword, user.Password))
      throw new UnauthorizedAccessException("Current password is incorrect");

    var hashedPassword = passwordHasher.HashPassword(changePasswordRequest.Password);
    await users.UpdatePasswordAsync(user.Id!, hashedPassword, cancellationToken);

    userService.ClearCachedUser(user.Id!);
    NotifyPasswordChangedFireAndForget(user.Id, user.Email);
  }

  private void NotifyPasswordChangedFireAndForget(string? userId, string email)
  {
    var changedAt = DateTime.UtcNow;
    _ = Task.Run(async () =>
    {
      try
      {
        var subject = "Rhema Bible — Password updated";
        var body = EmailTemplates.PasswordChanged(changedAt);
        await notificationService.SendAsync(email, subject, body, CancellationToken.None);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to send password-changed email for user {UserId}", userId);
      }
    });
  }

  public async Task<bool> VerifyEmailAsync(string email, string otpCode, OtpType otpType, CancellationToken cancellationToken)
  {
    var isValidOtp = await otpService.VerifyOtpAsync(email, otpCode, otpType, cancellationToken);

    if (isValidOtp)
    {
      var user = await userService.GetByEmailAsync(email, cancellationToken);
      if (user != null && user.Id != null)
      {
        await users.SetEmailVerifiedAsync(user.Id, true, cancellationToken);
        userService.ClearCachedUser(user.Id);
      }

      return true;
    }

    return false;
  }

  public async Task<bool> ResendOtpAsync(ResendOtpRequest resendOtpRequest, CancellationToken cancellationToken = default)
  {
    var user = await userService.GetByEmailAsync(resendOtpRequest.Email, cancellationToken);

    if (user == null)
      throw new ResourceNotFoundException($"User with email '{resendOtpRequest.Email}' was not found.");

    if (resendOtpRequest.OtpType == OtpType.EmailVerification && user.IsEmailVerified)
      throw new ConflictException("Email is already verified");

    if (user.Id == null)
      throw new BadRequestException("User ID is missing");

    await otpService.GenerateOtpAsync(user.Id, resendOtpRequest.OtpType, user.Email, cancellationToken);
    return true;
  }
}
