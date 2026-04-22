using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class UserFunctions(
  IUserApplicationService userService,
  IAiQuotaService aiQuotaService,
  IAccountDeletionService accountDeletionService,
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
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var callerId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(callerId))
        return await req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse<UserDto>.ErrorResponse("User id not found in token"));

      if (!string.Equals(callerId, id, StringComparison.Ordinal))
        return await req.CreateJsonResponse(HttpStatusCode.Forbidden, ApiResponse<UserDto>.ErrorResponse("You can only access your own user profile."));

      var user = await userService.GetByIdAsync(id, ct);
      if (user == null)
        return await req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<UserDto>.ErrorResponse("User not found"));

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(user.ToDto(aiQuotaService)));
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);

  [Function("User_GetMyProfile")]
  public Task<HttpResponseData> GetMyProfile(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/me/profile")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(userId))
        return await req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse<UserDto>.ErrorResponse("User id not found in token"));

      var user = await userService.GetByIdAsync(userId, ct);
      if (user == null)
        return await req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<UserDto>.ErrorResponse("User not found"));

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(user.ToDto(aiQuotaService)));
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);

  [Function("User_GetMySubscription")]
  public Task<HttpResponseData> GetMySubscription(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/me/subscription")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(userId))
        return await req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse<SubscriptionStatusDto>.ErrorResponse("User id not found in token"));

      var user = await userService.GetByIdAsync(userId, ct);
      if (user == null)
        return await req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<SubscriptionStatusDto>.ErrorResponse("User not found"));

      var subscriptionStatus = new SubscriptionStatusDto
      {
        SubscriptionType = user.SubscriptionType,
        LastUpdated = user.UpdatedAt,
        SubscriptionExpiresAt = user.SubscriptionExpiresAt
      };

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<SubscriptionStatusDto>.SuccessResponse(subscriptionStatus));
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);

  [Function("User_DeleteMyAccount")]
  public Task<HttpResponseData> DeleteMyAccount(
    [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/user/me")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(userId))
        return await req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse.Error<string>("User id not found in token"));

      await accountDeletionService.RequestDeletionAsync(userId, ct);
      return req.CreateResponse(HttpStatusCode.NoContent);
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);
}

