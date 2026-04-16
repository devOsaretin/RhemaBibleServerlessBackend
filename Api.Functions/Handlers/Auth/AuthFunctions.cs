using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Enums;

public class AuthFunctions(IAuthService authService, IFunctionTokenValidator tokenValidator, IHostEnvironment env, ILogger<AuthFunctions> logger)
{
  [Function("Auth_Login")]
  public Task<IActionResult> Login(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/login")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<LoginRequest>(ct);
      var response = await authService.LoginAsync(request, ct);
      return req.ApiResult(ApiResponse<AuthResponse>.SuccessResponse(response));
    }, cancellationToken, logger, env);

  [Function("Auth_Register")]
  public Task<IActionResult> Register(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/register")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<RegisterRequest>(ct);
      var response = await authService.RegisterAsync(request, ct);
      return req.ApiResult(ApiResponse<AuthResponse>.SuccessResponse(response));
    }, cancellationToken, logger, env);

  [Function("Auth_VerifyEmail")]
  public Task<IActionResult> VerifyEmail(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/email/verify")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<EmailVerifyRequest>(ct);
      var verify = await authService.VerifyEmailAsync(request.Email, request.Otp, OtpType.EmailVerification, ct);
      return req.ApiResult(ApiResponse.Success(verify));
    }, cancellationToken, logger, env);

  [Function("Auth_ResendOtp")]
  public Task<IActionResult> ResendOtp(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/otp/resend")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<ResendOtpRequest>(ct);
      var result = await authService.ResendOtpAsync(request, ct);
      return req.ApiResult(ApiResponse<bool>.SuccessResponse(result));
    }, cancellationToken, logger, env);

  [Function("Auth_ForgotPassword")]
  public Task<IActionResult> ForgotPassword(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/forgot")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<ForgotPasswordRequest>(ct);
      await authService.ForgotPasswordAsync(request, ct);
      return req.ApiResult(ApiResponse.Success<string>("If the email exists, a password reset code has been sent."));
    }, cancellationToken, logger, env);

  [Function("Auth_ResetPassword")]
  public Task<IActionResult> ResetPassword(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/reset")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<ResetPasswordRequest>(ct);
      await authService.ResetPasswordAsync(request, ct);
      return req.ApiResult(ApiResponse.Success<string>("Password has been reset successfully."));
    }, cancellationToken, logger, env);

  [Function("Auth_ChangePassword")]
  public Task<IActionResult> ChangePassword(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/change")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var request = await req.ReadRequiredJsonAsync<ChangePasswordRequest>(ct);
      await authService.ChangePasswordAsync(userId, request, ct);
      return req.ApiResult(ApiResponse.Success<string>("Password has been changed successfully."));
    }, cancellationToken, logger, env);

  [Function("Auth_RefreshToken")]
  public Task<IActionResult> RefreshToken(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/refresh-token")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<RefreshTokenRequest>(ct);
      var response = await authService.RefreshTokenAsync(request.RefreshToken, ct);
      return req.ApiResult(ApiResponse<AuthResponse>.SuccessResponse(response));
    }, cancellationToken, logger, env);
}

