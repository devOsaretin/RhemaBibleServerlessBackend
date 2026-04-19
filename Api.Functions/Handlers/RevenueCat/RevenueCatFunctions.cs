using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Domain.Enums;
using System.Globalization;

public class RevenueCatFunctions(
  IAdminService adminService,
  IUserApplicationService userService,
  IWebHookService webHookService,
  IOptions<RevenueCatSettings> revenueCatSettings,
  ILogger<RevenueCatFunctions> logger,
  IHostEnvironment env)
{
  private readonly RevenueCatSettings _revenueCatSettings = revenueCatSettings.Value;

  [Function("RevenueCat_Webhook")]
  public Task<HttpResponseData> HandleWebhook(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "revenuecat/webhook")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(
      req,
      async ct =>
      {
        var authHeader = req.Headers.TryGetValues("Authorization", out var values) ? values.FirstOrDefault() : null;
        if (!IsAuthorized(authHeader!))
          return req.CreateResponse(HttpStatusCode.Unauthorized);

        var webhookEvent = await req.ReadRequiredJsonAsync<RevenueCatWebHookEvent>(ct);
        if (webhookEvent?.Event == null)
        {
          logger.LogWarning("Received webhook with null event");
          return await req.CreateJsonResponse(HttpStatusCode.BadRequest, new { message = "Invalid webhook payload" });
        }

        var eventData = webhookEvent.Event;
        var appUserId = eventData.AppUserId;

        if (string.IsNullOrEmpty(appUserId))
        {
          logger.LogWarning("Received webhook with null app_user_id");
          return await req.CreateJsonResponse(HttpStatusCode.BadRequest, new { message = "app_user_id is required" });
        }

        logger.LogInformation(
          "Processing RevenueCat webhook: Type={Type}, AppUserId={AppUserId}",
          eventData.Type,
          appUserId);

        if (!await webHookService.TryMarkEventProcessedAsync(eventData.Id!, ct))
        {
          logger.LogWarning("Failed to mark event as processed");
          return req.CreateResponse(HttpStatusCode.OK);
        }

        var user = await userService.GetByIdAsync(appUserId, ct);
        if (user == null)
        {
          logger.LogWarning("User not found for app_user_id: {AppUserId}", appUserId);
          return req.CreateResponse(HttpStatusCode.OK);
        }

        var expiresAt = FromUnixMs(eventData.ExpirationAtMs);
        var isActive = expiresAt == null || expiresAt > DateTime.UtcNow;
        var newSubscriptionType = isActive ? MapProduct(eventData.ProductId) : SubscriptionType.Free;

        if (user.SubscriptionType != newSubscriptionType || user.SubscriptionExpiresAt != expiresAt)
        {
          var update = new UpdateSubscriptionDto
          {
            SubscriptionType = newSubscriptionType,
            SubscriptionExpiresAt = expiresAt
          };

          await adminService.UpdateUsersPlanAsync(user.Id!, update, ct);
          logger.LogInformation(
            "Updated user {UserId} to {Type}, expires {Expiry}",
            user.Id, newSubscriptionType, expiresAt);
        }

        return req.CreateResponse(HttpStatusCode.OK);
      },
      cancellationToken,
      logger,
      env,
      returnHttp200OnUnhandledException: true);

  private static DateTime? FromUnixMs(long? ms)
  {
    if (ms == null) return null;
    return DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).UtcDateTime;
  }

  private bool IsAuthorized(string authHeader)
  {
    if (string.IsNullOrEmpty(_revenueCatSettings.WebhookSecret))
      return true;

    if (string.IsNullOrWhiteSpace(authHeader))
      return false;

    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
      authHeader = authHeader["Bearer ".Length..].Trim();

    return authHeader == _revenueCatSettings.WebhookSecret;
  }

  private static SubscriptionType MapProduct(string? productId) =>
    productId?.ToLower(CultureInfo.InvariantCulture) switch
    {
      "monthly" => SubscriptionType.PremiumMonthly,
      "yearly" => SubscriptionType.PremiumYearly,
      _ => SubscriptionType.Free
    };
}
