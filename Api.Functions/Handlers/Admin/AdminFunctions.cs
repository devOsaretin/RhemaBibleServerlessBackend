using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class AdminFunctions(IAdminService adminService, IFunctionTokenValidator tokenValidator, IHostEnvironment env, ILogger<AdminFunctions> logger)
{
  [Function("Admin_GetProfile")]
  public Task<IActionResult> GetAdmin(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/profile")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var admin = await adminService.GetAdminAsync(userId);
      return req.ApiResult(ApiResponse<UserDto>.SuccessResponse(admin!));
    }, cancellationToken, logger, env);

  [Function("Admin_GetUsers")]
  public Task<IActionResult> GetUsersAsync(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);

      var query = new UserQueryDto
      {
        PageNumber = int.TryParse(req.Query["pageNumber"], out var pageNumber) ? pageNumber : 1,
        PageSize = int.TryParse(req.Query["pageSize"], out var pageSize) ? pageSize : 10,
        Status = req.Query["status"].FirstOrDefault(),
        SubscriptionType = req.Query["subscriptionType"].FirstOrDefault(),
        Search = req.Query["search"].FirstOrDefault()
      };

      var pagedResult = await adminService.GetUsersAsync(
        query.PageNumber,
        query.PageSize,
        query.Status,
        query.SubscriptionType,
        query.Search);

      return req.ApiResult(ApiResponse<UserDto?>.FromPagedResult(pagedResult));
    }, cancellationToken, logger, env);

  [Function("Admin_GetUser")]
  public Task<IActionResult> GetUserAsync(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users/{userId}")] HttpRequest req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      var user = await adminService.GetUserAsync(userId, ct);
      if (user == null)
        return req.ApiResult(ApiResponse<UserDto>.ErrorResponse("User not found"), System.Net.HttpStatusCode.NotFound);

      return req.ApiResult(ApiResponse<UserDto>.SuccessResponse(user));
    }, cancellationToken, logger, env);

  [Function("Admin_ActivateUser")]
  public Task<IActionResult> ActivateUser(
    [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId}/activate")] HttpRequest req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      return req.ApiResult(ApiResponse<UserDto>.SuccessResponse(await adminService.ActivateUserAsync(userId)));
    }, cancellationToken, logger, env);

  [Function("Admin_DeactivateUser")]
  public Task<IActionResult> DeactivateUser(
    [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId}/deactivate")] HttpRequest req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      return req.ApiResult(ApiResponse<UserDto>.SuccessResponse(await adminService.DeactivateUserAsync(userId)));
    }, cancellationToken, logger, env);

  [Function("Admin_UpdateSubscription")]
  public Task<IActionResult> UpdateSubscription(
    [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{userId}/subscription")] HttpRequest req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      var subscriptionDto = await req.ReadRequiredJsonAsync<UpdateSubscriptionDto>(ct);
      if (!IsValid(subscriptionDto))
        return req.ApiResult(ApiResponse<UserDto>.ErrorResponse("Invalid subscription data"), System.Net.HttpStatusCode.BadRequest);

      var updated = await adminService.UpdateUsersPlanAsync(userId, subscriptionDto, ct);
      return req.ApiResult(ApiResponse<UserDto>.SuccessResponse(updated));
    }, cancellationToken, logger, env);

  [Function("Admin_GetDashboardAnalytics")]
  public Task<IActionResult> GetDashboardAnalytics(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/dashboard")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      return req.ApiResult(ApiResponse<DashboardAnalyticsDto>.SuccessResponse(await adminService.GetDashboardAnalyticsAsync()));
    }, cancellationToken, logger, env);

  [Function("Admin_GetDashboardStatistics")]
  public Task<IActionResult> GetDashboardStatistics(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/dashboard/statistics")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      var format = req.Query["format"].FirstOrDefault();
      var stats = await adminService.GetDashboardStatisticsExportAsync(ct);

      if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
      {
        var csv = DashboardStatisticsCsvFormatter.ToCsv(stats);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var fileName = $"rhemapp-dashboard-statistics-{DateTime.UtcNow:yyyy-MM-dd-HHmm}Z.csv";
        return new FileContentResult(bytes, "text/csv; charset=utf-8")
        {
          FileDownloadName = fileName
        };
      }

      return req.ApiResult(ApiResponse<DashboardStatisticsExportDto>.SuccessResponse(stats));
    }, cancellationToken, logger, env);

  [Function("Admin_GetUserAiQuota")]
  public Task<IActionResult> GetUserAiQuota(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users/{userId}/ai-quota")] HttpRequest req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      var dto = await adminService.GetUserAiQuotaAsync(userId, ct);
      return req.ApiResult(ApiResponse<AdminUserAiQuotaDto>.SuccessResponse(dto));
    }, cancellationToken, logger, env);

  [Function("Admin_ResetUserAiQuota")]
  public Task<IActionResult> ResetUserAiQuota(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{userId}/ai-quota/reset")] HttpRequest req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      var dto = await adminService.ResetUserAiQuotaAsync(userId, ct);
      return req.ApiResult(ApiResponse<AdminUserAiQuotaDto>.SuccessResponse(dto));
    }, cancellationToken, logger, env);

  [Function("Admin_SetUserAiQuotaRemaining")]
  public Task<IActionResult> SetUserAiQuotaRemaining(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{userId}/ai-quota/set-remaining")] HttpRequest req,
    string userId,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      await req.RequireAdminClerkUserAsync(tokenValidator, ct);
      var remainingThisMonth = int.TryParse(req.Query["remainingThisMonth"], out var remaining) ? remaining : 0;
      var dto = await adminService.SetUserAiQuotaRemainingAsync(userId, remainingThisMonth, ct);
      return req.ApiResult(ApiResponse<AdminUserAiQuotaDto>.SuccessResponse(dto));
    }, cancellationToken, logger, env);

  private static bool IsValid<T>(T model)
  {
    var validationResults = new List<ValidationResult>();
    return Validator.TryValidateObject(model!, new ValidationContext(model!), validationResults, true);
  }
}

