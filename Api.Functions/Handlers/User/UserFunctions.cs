using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class UserFunctions(
  IUserService userService,
  IAiQuotaService aiQuotaService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  IHostEnvironment env,
  ILogger<UserFunctions> logger)
{
  [Function("User_GetById")]
  public Task<HttpResponseData> GetById(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/{id}")] HttpRequestData req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      var callerId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(callerId))
        return req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse<UserDto>.ErrorResponse("User id not found in token"));

      if (!string.Equals(callerId, id, StringComparison.Ordinal))
        return req.CreateJsonResponse(HttpStatusCode.Forbidden, ApiResponse<UserDto>.ErrorResponse("You can only access your own user profile."));

      var user = await userService.GetByIdAsync(id, ct);
      if (user == null)
        return req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<UserDto>.ErrorResponse("User not found"));

      return req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(user.ToDto(aiQuotaService)));
    }, cancellationToken, logger, env);

  [Function("User_GetMyProfile")]
  public Task<HttpResponseData> GetMyProfile(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/me/profile")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      var userId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(userId))
        return req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse<UserDto>.ErrorResponse("User id not found in token"));

      var user = await userService.GetByIdAsync(userId, ct);
      if (user == null)
        return req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<UserDto>.ErrorResponse("User not found"));

      return req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(user.ToDto(aiQuotaService)));
    }, cancellationToken, logger, env);

  [Function("User_GetMySubscription")]
  public Task<HttpResponseData> GetMySubscription(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/me/subscription")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      var userId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(userId))
        return req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse<SubscriptionStatusDto>.ErrorResponse("User id not found in token"));

      var user = await userService.GetByIdAsync(userId, ct);
      if (user == null)
        return req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<SubscriptionStatusDto>.ErrorResponse("User not found"));

      var subscriptionStatus = new SubscriptionStatusDto
      {
        SubscriptionType = user.SubscriptionType,
        LastUpdated = user.UpdatedAt,
        SubscriptionExpiresAt = user.SubscriptionExpiresAt
      };

      return req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<SubscriptionStatusDto>.SuccessResponse(subscriptionStatus));
    }, cancellationToken, logger, env);
}

