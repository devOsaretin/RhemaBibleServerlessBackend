using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Enums;

public class AuthFunctions(
  IAuthService authService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  IHostEnvironment env,
  ILogger<AuthFunctions> logger)
{
  [Function("Auth_Login")]
  public Task<HttpResponseData> Login(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/login")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<LoginRequest>(ct);
      var response = await authService.LoginAsync(request, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<AuthResponse>.SuccessResponse(response));
    }, cancellationToken, logger, env);

  [Function("Auth_Register")]
  public Task<HttpResponseData> Register(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/register")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<RegisterRequest>(ct);
      var response = await authService.RegisterAsync(request, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<AuthResponse>.SuccessResponse(response));
    }, cancellationToken, logger, env);

  [Function("Auth_VerifyEmail")]
  public Task<HttpResponseData> VerifyEmail(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/email/verify")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<EmailVerifyRequest>(ct);
      var verify = await authService.VerifyEmailAsync(request.Email, request.Otp, OtpType.EmailVerification, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse.Success(verify));
    }, cancellationToken, logger, env);

  [Function("Auth_ResendOtp")]
  public Task<HttpResponseData> ResendOtp(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/otp/resend")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<ResendOtpRequest>(ct);
      var result = await authService.ResendOtpAsync(request, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<bool>.SuccessResponse(result));
    }, cancellationToken, logger, env);

  [Function("Auth_ForgotPassword")]
  public Task<HttpResponseData> ForgotPassword(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/forgot")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<ForgotPasswordRequest>(ct);
      await authService.ForgotPasswordAsync(request, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse.Success<string>("If the email exists, a password reset code has been sent."));
    }, cancellationToken, logger, env);

  [Function("Auth_ResetPassword")]
  public Task<HttpResponseData> ResetPassword(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/reset")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<ResetPasswordRequest>(ct);
      await authService.ResetPasswordAsync(request, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse.Success<string>("Password has been reset successfully."));
    }, cancellationToken, logger, env);

  [Function("Auth_ChangePassword")]
  public Task<HttpResponseData> ChangePassword(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/change")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var request = await req.ReadRequiredJsonAsync<ChangePasswordRequest>(ct);
      await authService.ChangePasswordAsync(userId, request, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse.Success<string>("Password has been changed successfully."));
    }, tokenValidator, principalAccessor, cancellationToken, logger, env);

  [Function("Auth_RefreshToken")]
  public Task<HttpResponseData> RefreshToken(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/refresh-token")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var request = await req.ReadRequiredJsonAsync<RefreshTokenRequest>(ct);
      var response = await authService.RefreshTokenAsync(request.RefreshToken, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<AuthResponse>.SuccessResponse(response));
    }, cancellationToken, logger, env);
}

