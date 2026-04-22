using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class AdminFunctions(
  IAdminService adminService,
  IUserApplicationService userService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  IHostEnvironment env,
  ILogger<AdminFunctions> logger)
{
  [Function("Admin_GetProfile")]
  public Task<HttpResponseData> GetAdmin(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/profile")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var admin = await adminService.GetAdminAsync(userId);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(admin!));
    }, cancellationToken, logger, env);

  [Function("Admin_GetUsers")]
  public Task<HttpResponseData> GetUsersAsync(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);

      var queryMap = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
      var query = new UserQueryDto
      {
        PageNumber = int.TryParse(queryMap.TryGetValue("pageNumber", out var pn) ? pn.ToString() : null, out var pageNumber) ? pageNumber : 1,
        PageSize = int.TryParse(queryMap.TryGetValue("pageSize", out var ps) ? ps.ToString() : null, out var pageSize) ? pageSize : 10,
        Status = queryMap.TryGetValue("status", out var st) ? st.ToString() : null,
        SubscriptionType = queryMap.TryGetValue("subscriptionType", out var sub) ? sub.ToString() : null,
        Search = queryMap.TryGetValue("search", out var search) ? search.ToString() : null
      };

      var pagedResult = await adminService.GetUsersAsync(
        query.PageNumber,
        query.PageSize,
        query.Status,
        query.SubscriptionType,
        query.Search);

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto?>.FromPagedResult(pagedResult));
    }, cancellationToken, logger, env);

  [Function("Admin_GetUser")]
  public Task<HttpResponseData> GetUserAsync(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users/{userId}")] HttpRequestData req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      var user = await adminService.GetUserAsync(userId, ct);
      if (user == null)
        return await req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<UserDto>.ErrorResponse("User not found"));

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(user));
    }, cancellationToken, logger, env);

  [Function("Admin_ActivateUser")]
  public Task<HttpResponseData> ActivateUser(
    [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId}/activate")] HttpRequestData req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(await adminService.ActivateUserAsync(userId)));
    }, cancellationToken, logger, env);

  [Function("Admin_DeactivateUser")]
  public Task<HttpResponseData> DeactivateUser(
    [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId}/deactivate")] HttpRequestData req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(await adminService.DeactivateUserAsync(userId)));
    }, cancellationToken, logger, env);

  [Function("Admin_UpdateSubscription")]
  public Task<HttpResponseData> UpdateSubscription(
    [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId}/subscription")] HttpRequestData req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      var subscriptionDto = await req.ReadRequiredJsonAsync<UpdateSubscriptionDto>(ct);
      if (!IsValid(subscriptionDto))
        return await req.CreateJsonResponse(HttpStatusCode.BadRequest, ApiResponse<UserDto>.ErrorResponse("Invalid subscription data"));

      var updated = await adminService.UpdateUsersPlanAsync(userId, subscriptionDto, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<UserDto>.SuccessResponse(updated));
    }, cancellationToken, logger, env);

  [Function("Admin_GetDashboardAnalytics")]
  public Task<HttpResponseData> GetDashboardAnalytics(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/dashboard")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<DashboardAnalyticsDto>.SuccessResponse(await adminService.GetDashboardAnalyticsAsync()));
    }, cancellationToken, logger, env);

  [Function("Admin_GetDashboardStatistics")]
  public Task<HttpResponseData> GetDashboardStatistics(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/dashboard/statistics")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      var queryMap = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
      var format = queryMap.TryGetValue("format", out var fmt) ? fmt.ToString() : null;
      var stats = await adminService.GetDashboardStatisticsExportAsync(ct);

      if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
      {
        var csv = DashboardStatisticsCsvFormatter.ToCsv(stats);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var fileName = $"rhemapp-dashboard-statistics-{DateTime.UtcNow:yyyy-MM-dd-HHmm}Z.csv";
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/csv; charset=utf-8");
        res.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        await res.Body.WriteAsync(bytes, ct);
        return res;
      }

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<DashboardStatisticsExportDto>.SuccessResponse(stats));
    }, cancellationToken, logger, env);

  [Function("Admin_GetUserAiQuota")]
  public Task<HttpResponseData> GetUserAiQuota(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users/{userId}/ai-quota")] HttpRequestData req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      var dto = await adminService.GetUserAiQuotaAsync(userId, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<AdminUserAiQuotaDto>.SuccessResponse(dto));
    }, cancellationToken, logger, env);

  [Function("Admin_ResetUserAiQuota")]
  public Task<HttpResponseData> ResetUserAiQuota(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{userId}/ai-quota/reset")] HttpRequestData req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      var dto = await adminService.ResetUserAiQuotaAsync(userId, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<AdminUserAiQuotaDto>.SuccessResponse(dto));
    }, cancellationToken, logger, env);

  [Function("Admin_SetUserAiQuotaRemaining")]
  public Task<HttpResponseData> SetUserAiQuotaRemaining(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{userId}/ai-quota/set-remaining")] HttpRequestData req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, principalAccessor, userService, ct);
      var queryMap = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
      var remainingThisMonth = int.TryParse(queryMap.TryGetValue("remainingThisMonth", out var rem) ? rem.ToString() : null, out var remaining) ? remaining : 0;
      var dto = await adminService.SetUserAiQuotaRemainingAsync(userId, remainingThisMonth, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<AdminUserAiQuotaDto>.SuccessResponse(dto));
    }, cancellationToken, logger, env);

  private static bool IsValid<T>(T model)
  {
    var validationResults = new List<ValidationResult>();
    return Validator.TryValidateObject(model!, new ValidationContext(model!), validationResults, true);
  }
}

