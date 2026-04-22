public interface IAccountDeletionService
{
  Task DeleteMyAccountAsync(string userId, CancellationToken cancellationToken = default);
}

