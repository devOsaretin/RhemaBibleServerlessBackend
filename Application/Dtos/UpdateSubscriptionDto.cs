using System.ComponentModel.DataAnnotations;


public class UpdateSubscriptionDto
{
    [Required(ErrorMessage = "Subscription type is required")]
    [EnumDataType(typeof(SubscriptionType), ErrorMessage = "Invalid subscription type. Valid values include Free, Premium, PremiumMonthly, PremiumYearly")]
    public SubscriptionType SubscriptionType { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
}
