using System.Security.Claims;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Models;

public class RecentActivityFunctions(
  IRecentActivityService recentActivityService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  IHostEnvironment env,
  ILogger<RecentActivityFunctions> logger)
{
  [Function("RecentActivity_GetRecentActivitiesByUser")]
  public Task<HttpResponseData> GetRecentActivitiesByUser(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/recentactivity")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      var activities = await recentActivityService.GetRecentActivitiesByUserAsync(userId);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<IReadOnlyList<RecentActivity>>.SuccessResponse(activities));
    }, tokenValidator, principalAccessor, cancellationToken, logger, env);

  [Function("RecentActivity_AddUserActivity")]
  public Task<HttpResponseData> AddUserActivity(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/recentactivity")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var recentActivityDto = await req.ReadRequiredJsonAsync<AddActivityDto>(ct);

      var newActivity = new RecentActivity
      {
        AuthId = userId,
        Title = recentActivityDto.Title,
        ActivityType = recentActivityDto.ActivityType
      };

      var activity = await recentActivityService.AddActivityByUser(newActivity);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<RecentActivity>.SuccessResponse(activity));
    }, tokenValidator, principalAccessor, cancellationToken, logger, env);
}

