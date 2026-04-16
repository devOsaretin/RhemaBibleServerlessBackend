

public record SubscriptionStatusDto
{
    public SubscriptionType SubscriptionType { get; init; }
    public DateTime? LastUpdated { get; init; }
    public DateTime? SubscriptionExpiresAt { get; init; }
    
    public bool IsPremium => SubscriptionType.IsPremium() && 
                             (SubscriptionType == SubscriptionType.Premium || 
                              SubscriptionExpiresAt == null || 
                              SubscriptionExpiresAt > DateTime.UtcNow);
    
    public bool IsMonthly => SubscriptionType == SubscriptionType.PremiumMonthly;
    
    public bool IsYearly => SubscriptionType == SubscriptionType.PremiumYearly;
    
    public bool IsLegacyPremium => SubscriptionType == SubscriptionType.Premium;
}

