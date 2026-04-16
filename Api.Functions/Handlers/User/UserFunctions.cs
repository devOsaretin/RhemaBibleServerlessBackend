using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class UserFunctions(IUserService userService, IAiQuotaService aiQuotaService, IFunctionTokenValidator tokenValidator, IHostEnvironment env, ILogger<UserFunctions> logger)
{
  [Function("User_GetById")]
  public Task<IActionResult> GetById(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/{id}")] HttpRequest req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      req.RequireLocalJwtUser(tokenValidator);

      var user = await userService.GetByIdAsync(id, ct);
      if (user == null)
        return req.ApiResult(ApiResponse<UserDto>.ErrorResponse("User not found"), HttpStatusCode.NotFound);

      return req.ApiResult(ApiResponse<UserDto>.SuccessResponse(user.ToDto(aiQuotaService)));
    }, cancellationToken, logger, env);

  [Function("User_GetMyProfile")]
  public Task<IActionResult> GetMyProfile(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/me/profile")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (string.IsNullOrEmpty(userId))
        return req.ApiResult(ApiResponse<UserDto>.ErrorResponse("Clerk ID not found"), HttpStatusCode.Unauthorized);

      var user = await userService.GetByIdAsync(userId, ct);
      if (user == null)
        return req.ApiResult(ApiResponse<UserDto>.ErrorResponse("User not found"), HttpStatusCode.NotFound);

      return req.ApiResult(ApiResponse<UserDto>.SuccessResponse(user.ToDto(aiQuotaService)));
    }, cancellationToken, logger, env);

  [Function("User_GetMySubscription")]
  public Task<IActionResult> GetMySubscription(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/me/subscription")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (string.IsNullOrEmpty(userId))
        return req.ApiResult(ApiResponse<SubscriptionStatusDto>.ErrorResponse("Clerk ID not found"), HttpStatusCode.Unauthorized);

      var user = await userService.GetByIdAsync(userId, ct);
      if (user == null)
        return req.ApiResult(ApiResponse<SubscriptionStatusDto>.ErrorResponse("User not found"), HttpStatusCode.NotFound);

      var subscriptionStatus = new SubscriptionStatusDto
      {
        SubscriptionType = user.SubscriptionType,
        LastUpdated = user.UpdatedAt,
        SubscriptionExpiresAt = user.SubscriptionExpiresAt
      };

      return req.ApiResult(ApiResponse<SubscriptionStatusDto>.SuccessResponse(subscriptionStatus));
    }, cancellationToken, logger, env);
}

