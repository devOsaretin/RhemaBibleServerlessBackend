public static class UserExtensions
{
    public static UserDto ToDto(this User user, IAiQuotaService aiQuotaService, bool includeAiUsage = true)
    {
        return new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Status: user.Status,
            SubscriptionType: user.SubscriptionType,
            CreatedAt: user.CreatedAt,
            Email: user.Email,
            ImageUrl: user.ImageUrl,
            IsEmailVerified: user.IsEmailVerified,
            SubscriptionExpiresAt: user.SubscriptionExpiresAt,
            AiUsage: includeAiUsage ? aiQuotaService.BuildUsageSnapshot(user) : null
        );
    }

    public static bool IsPremium(this SubscriptionType subscriptionType)
    {
        return subscriptionType == SubscriptionType.Premium ||
               subscriptionType == SubscriptionType.PremiumMonthly ||
               subscriptionType == SubscriptionType.PremiumYearly;
    }

    public static bool HasActivePremiumSubscription(this User user)
    {
        if (!user.SubscriptionType.IsPremium())
        {
            return false;
        }

        if (user.SubscriptionType == SubscriptionType.Premium)
        {
            return true;
        }

        if (user.SubscriptionExpiresAt == null)
        {
            return true;
        }

        return user.SubscriptionExpiresAt.Value.ToUniversalTime() > DateTime.UtcNow;
    }
}
