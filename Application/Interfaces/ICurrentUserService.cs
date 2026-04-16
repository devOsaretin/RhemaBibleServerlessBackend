public interface ICurrentUserService
{
  Task<User> GetUserAsync(CancellationToken cancellationToken);
  string GetCurrentUserId();
}
