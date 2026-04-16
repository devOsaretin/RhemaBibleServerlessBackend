using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Models;

public class RecentActivityFunctions(IRecentActivityService recentActivityService, IFunctionTokenValidator tokenValidator, IHostEnvironment env, ILogger<RecentActivityFunctions> logger)
{
  [Function("RecentActivity_GetRecentActivitiesByUser")]
  public Task<IActionResult> GetRecentActivitiesByUser(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/recentactivity")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      var activities = await recentActivityService.GetRecentActivitiesByUserAsync(userId);
      return req.ApiResult(ApiResponse<IReadOnlyList<RecentActivity>>.SuccessResponse(activities));
    }, cancellationToken, logger, env);

  [Function("RecentActivity_AddUserActivity")]
  public Task<IActionResult> AddUserActivity(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/recentactivity")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var recentActivityDto = await req.ReadRequiredJsonAsync<AddActivityDto>(ct);

      var newActivity = new RecentActivity
      {
        AuthId = userId,
        Title = recentActivityDto.Title,
        ActivityType = recentActivityDto.ActivityType
      };

      var activity = await recentActivityService.AddActivityByUser(newActivity);
      return req.ApiResult(ApiResponse<RecentActivity>.SuccessResponse(activity));
    }, cancellationToken, logger, env);
}

