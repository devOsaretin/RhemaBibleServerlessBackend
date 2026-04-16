using System.Text.Json.Serialization;



public class RevenueCatWebHookEvent
{
    [JsonPropertyName("event")]
    public EventData? Event { get; set; }
}

public class EventData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("app_user_id")]
    public string? AppUserId { get; set; }

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("period_type")]
    public string? PeriodType { get; set; }

    [JsonPropertyName("purchased_at_ms")]
    public long? PurchasedAtMs { get; set; }

    [JsonPropertyName("expiration_at_ms")]
    public long? ExpirationAtMs { get; set; }

    [JsonPropertyName("entitlements")]
    public Dictionary<string, EntitlementInfo>? Entitlements { get; set; }

    [JsonPropertyName("subscriber")]
    public SubscriberInfo? Subscriber { get; set; }
}

public class EntitlementInfo
{
    [JsonPropertyName("expires_date")]
    public string? ExpiresDate { get; set; }

    [JsonPropertyName("product_identifier")]
    public string? ProductIdentifier { get; set; }

    [JsonPropertyName("purchase_date")]
    public string? PurchaseDate { get; set; }
}

public class SubscriberInfo
{
    [JsonPropertyName("entitlements")]
    public Dictionary<string, EntitlementInfo>? Entitlements { get; set; }

    [JsonPropertyName("first_seen")]
    public string? FirstSeen { get; set; }

    [JsonPropertyName("last_seen")]
    public string? LastSeen { get; set; }

    [JsonPropertyName("management_url")]
    public string? ManagementUrl { get; set; }

    [JsonPropertyName("original_app_user_id")]
    public string? OriginalAppUserId { get; set; }

    [JsonPropertyName("original_application_version")]
    public string? OriginalApplicationVersion { get; set; }

    [JsonPropertyName("other_purchases")]
    public Dictionary<string, object>? OtherPurchases { get; set; }

    [JsonPropertyName("subscriptions")]
    public Dictionary<string, SubscriptionInfo>? Subscriptions { get; set; }
}

public class SubscriptionInfo
{
    public SubscriptionInfo() { }

    [JsonPropertyName("billing_issues_detected_at")]
    public string? BillingIssuesDetectedAt { get; set; }

    [JsonPropertyName("expires_date")]
    public string? ExpiresDate { get; set; }

    [JsonPropertyName("grace_period_expires_date")]
    public string? GracePeriodExpiresDate { get; set; }

    [JsonPropertyName("is_sandbox")]
    public bool IsSandbox { get; set; }

    [JsonPropertyName("original_purchase_date")]
    public string? OriginalPurchaseDate { get; set; }

    [JsonPropertyName("period_type")]
    public string? PeriodType { get; set; }

    [JsonPropertyName("product_identifier")]
    public string? ProductIdentifier { get; set; }

    [JsonPropertyName("purchase_date")]
    public string? PurchaseDate { get; set; }

    [JsonPropertyName("store")]
    public string? Store { get; set; }

    [JsonPropertyName("unsubscribe_detected_at")]
    public string? UnsubscribeDetectedAt { get; set; }
}
