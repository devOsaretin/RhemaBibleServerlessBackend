using System.Globalization;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Domain.Enums;
using RhemaBibleAppServerless.Domain.Models;
using RhemaBibleAppServerless.Shared.Helpers;

public class RevenueCatFunctions(
  IAdminService adminService,
  IUserApplicationService userService,
  IWebHookService webHookService,
  IServiceBusService serviceBusService,
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
        var nowUtc = DateTime.UtcNow;
        SubscriptionType newSubscriptionType;
        var mappedProduct = MapProduct(eventData.ProductId);
        var eventKind = eventData.Type?.ToLower(CultureInfo.InvariantCulture) ?? "";

        bool? purchaseRenewalUncancelActive = null;
        bool? cancellationStillHasAccess = null;

        switch (eventKind)
        {
          case "initial_purchase":
          case "non_renewing_purchase":
          case "uncancellation":
          case "renewal":

            var isActive = expiresAt == null || expiresAt > DateTime.UtcNow;
            purchaseRenewalUncancelActive = isActive;
            newSubscriptionType = isActive ? mappedProduct : SubscriptionType.Free;

            logger.LogInformation(
              "ProductId from RevenueCat: '{ProductType}', IsActive: {IsActive}, ExpiresAt: {ExpiresAt}",
              mappedProduct,
              isActive,
              expiresAt);

            break;

          case "upgrade":
            newSubscriptionType = mappedProduct;
            break;

          case "downgrade":
            newSubscriptionType = SubscriptionType.Free;
            break;

          case "billing_issue":
            newSubscriptionType = user.SubscriptionType;
            break;

          case "cancellation":
            var stillHasAccess = expiresAt != null && expiresAt > nowUtc;
            cancellationStillHasAccess = stillHasAccess;
            newSubscriptionType = stillHasAccess ? user.SubscriptionType : SubscriptionType.Free;
            break;

          case "expiration":
            newSubscriptionType = SubscriptionType.Free;
            break;

          default:
            logger.LogWarning("Unknown event type: {EventType}", eventData.Type);
            return req.CreateResponse(HttpStatusCode.OK);
        }

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
            user.Id,
            newSubscriptionType,
            expiresAt);
        }

        await QueueSubscriptionLifecycleEmailsAsync(
          eventKind,
          eventData,
          user,
          newSubscriptionType,
          mappedProduct,
          expiresAt,
          purchaseRenewalUncancelActive,
          cancellationStillHasAccess,
          ct);

        return req.CreateResponse(HttpStatusCode.OK);
      },
      cancellationToken,
      logger,
      env,
      returnHttp200OnUnhandledException: true);

  private async Task QueueSubscriptionLifecycleEmailsAsync(
    string eventKind,
    EventData eventData,
    User user,
    SubscriptionType newSubscriptionType,
    SubscriptionType mappedProduct,
    DateTime? expiresAt,
    bool? purchaseRenewalUncancelActive,
    bool? cancellationStillHasAccess,
    CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(user.Email))
    {
      logger.LogWarning("Skipping subscription email: user {UserId} has no email", user.Id);
      return;
    }

    var managementUrl = eventData.Subscriber?.ManagementUrl;

    switch (eventKind)
    {
      case "billing_issue":
        {
          var grace = TryGetGracePeriodEndUtc(eventData);
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Action needed: billing problem",
            EmailTemplates.BillingIssue(grace, managementUrl),
            ct);
          break;
        }

      case "expiration":
        await EnqueueSubscriptionEmailAsync(
          user.Email,
          "Rhema Bible — Subscription expired",
          EmailTemplates.SubscriptionExpired(),
          ct);
        break;

      case "downgrade":
        await EnqueueSubscriptionEmailAsync(
          user.Email,
          "Rhema Bible — Plan updated",
          EmailTemplates.SubscriptionDowngraded(managementUrl),
          ct);
        break;

      case "upgrade":
        await EnqueueSubscriptionEmailAsync(
          user.Email,
          "Rhema Bible — Subscription upgraded",
          EmailTemplates.SubscriptionUpgraded(newSubscriptionType, expiresAt, managementUrl),
          ct);
        break;

      case "cancellation":
        if (cancellationStillHasAccess == true && expiresAt.HasValue)
        {
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Auto-renew turned off",
            EmailTemplates.SubscriptionAutoRenewCancelled(expiresAt.Value, managementUrl),
            ct);
        }
        else
        {
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Subscription ended",
            EmailTemplates.SubscriptionEndedAfterCancellation(managementUrl),
            ct);
        }

        break;

      case "initial_purchase":
      case "non_renewing_purchase":
        if (purchaseRenewalUncancelActive == true)
        {
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Subscription activated",
            EmailTemplates.SubscriptionPurchased(mappedProduct, expiresAt),
            ct);
        }
        else
        {
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Subscription expired",
            EmailTemplates.SubscriptionExpired(),
            ct);
        }

        break;

      case "renewal":
        if (purchaseRenewalUncancelActive == true)
        {
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Subscription renewed",
            EmailTemplates.SubscriptionRenewed(mappedProduct, expiresAt),
            ct);
        }
        else
        {
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Subscription expired",
            EmailTemplates.SubscriptionExpired(),
            ct);
        }

        break;

      case "uncancellation":
        if (purchaseRenewalUncancelActive == true)
        {
          await EnqueueSubscriptionEmailAsync(
            user.Email,
            "Rhema Bible — Subscription continues",
            EmailTemplates.SubscriptionContinues(newSubscriptionType, expiresAt, managementUrl),
            ct);
        }

        break;
    }
  }

  private async Task EnqueueSubscriptionEmailAsync(string recipient, string subject, string htmlBody, CancellationToken ct)
  {
    var queueMessage = new EmailRequestFromQueueDto
    {
      Recipient = recipient.Trim(),
      Subject = subject,
      Body = htmlBody
    };

    await serviceBusService.PublishAsync(queueMessage, QueueNames.Email, ct);
    logger.LogInformation("Queued subscription lifecycle email: {Subject}", subject);
  }

  private static DateTime? TryGetGracePeriodEndUtc(EventData e)
  {
    var sub = FindSubscriptionInfo(e);
    if (sub == null) return null;
    return ParseRevenueCatIsoDate(sub.GracePeriodExpiresDate);
  }

  private static SubscriptionInfo? FindSubscriptionInfo(EventData e)
  {
    if (e.Subscriber?.Subscriptions == null || string.IsNullOrEmpty(e.ProductId))
      return null;

    if (e.Subscriber.Subscriptions.TryGetValue(e.ProductId, out var direct))
      return direct;

    foreach (var kv in e.Subscriber.Subscriptions)
    {
      if (string.Equals(kv.Key, e.ProductId, StringComparison.OrdinalIgnoreCase))
        return kv.Value;
    }

    return e.Subscriber.Subscriptions.Values.FirstOrDefault();
  }

  private static DateTime? ParseRevenueCatIsoDate(string? iso)
  {
    if (string.IsNullOrWhiteSpace(iso)) return null;
    if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
      return null;

    return dt.Kind == DateTimeKind.Unspecified
      ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
      : dt.ToUniversalTime();
  }

  private static DateTime? FromUnixMs(long? ms)
  {
    if (ms == null) return null;
    return DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).UtcDateTime;
  }

  private bool IsAuthorized(string authHeader)
  {
    if (string.IsNullOrEmpty(_revenueCatSettings.WebhookSecret))
      return false;

    if (string.IsNullOrWhiteSpace(authHeader))
      return false;

    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
      authHeader = authHeader["Bearer ".Length..].Trim();

    return authHeader == _revenueCatSettings.WebhookSecret;
  }

  private SubscriptionType MapProduct(string? productId)
  {
    if (string.IsNullOrEmpty(productId)) return SubscriptionType.Free;

    var lowerId = productId.ToLower(CultureInfo.InvariantCulture);

    // Match your actual product ID from RevenueCat
    switch (lowerId)
    {
      case "premium_monthly":
      case "monthly":

        return SubscriptionType.PremiumMonthly;

      case "premium_yearly":
      case "yearly":

        return SubscriptionType.PremiumYearly;

      default:
        logger.LogWarning("Unknown ProductId: {ProductId}", productId);
        return SubscriptionType.Free;
    }
  }
}
