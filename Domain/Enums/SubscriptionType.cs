namespace RhemaBibleAppServerless.Domain.Enums;

public enum SubscriptionType
{
  Free,
  Premium, // Legacy - will be migrated to PremiumMonthly or PremiumYearly
  PremiumMonthly,
  PremiumYearly
}