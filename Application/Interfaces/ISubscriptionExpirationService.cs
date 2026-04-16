


public interface ISubscriptionExpirationService
{
    Task<int> ExpireSubscriptionsAsync(CancellationToken cancellationToken = default);
}

