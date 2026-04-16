

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest forgotPasswordRequest, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest changePasswordRequest, CancellationToken cancellationToken = default);
    Task<string> GenerateTokenAsync(User user, string email, int? expirationMinutes = null);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> VerifyEmailAsync(string email, string otpCode, OtpType otpType, CancellationToken cancellationToken);
    Task<bool> ResendOtpAsync(ResendOtpRequest resendOtpRequest, CancellationToken cancellationToken = default);


}