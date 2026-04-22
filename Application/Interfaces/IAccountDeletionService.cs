public interface IAccountDeletionService
{
  Task RequestDeletionAsync(string userId, CancellationToken cancellationToken = default);
  Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

