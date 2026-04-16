using System.Text.Json.Serialization;


public record UserDto
(
    string? Id,
    string Email,
    string? FirstName,
    string? LastName,
    SubscriptionType SubscriptionType,
    AccountStatus Status,
    string? ImageUrl,
    DateTime CreatedAt,
    bool IsEmailVerified,
    DateTime? SubscriptionExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AiUsageDto? AiUsage = null
);

