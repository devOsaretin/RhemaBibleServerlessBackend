using System.Security.Claims;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Enums;


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


  [Function("ProcessActivity")]
  public async Task AddUserActivityFromQueue(
    [ServiceBusTrigger(QueueNames.Activity, Connection = "ServiceBus:ConnectionString")] string messageBody)
  {
    var addActivityToQueueDto = ParseQueueMessage.Parse<AddActivityToQueueDto>(messageBody);
    Enum.TryParse<ActivityType>(addActivityToQueueDto.ActivityType, out var activityType);
    var newActivity = new RecentActivity
    {
      AuthId = addActivityToQueueDto.AuthId,
      Title = addActivityToQueueDto.Title,
      ActivityType = activityType
    };

    await recentActivityService.AddActivityByUser(newActivity);
    logger.LogInformation("Processing activity for user {UserId}", addActivityToQueueDto.AuthId);
  }

  
}

