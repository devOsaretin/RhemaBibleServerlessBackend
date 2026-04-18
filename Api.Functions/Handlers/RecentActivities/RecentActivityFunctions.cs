using System.Security.Claims;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


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


}

