/// <summary>Application-level user operations (cache, find-or-create, etc.). Not raw database access.</summary>
public interface IUserApplicationService
{
  Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);
  Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

  Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
  Task<User> FindOrCreateAsync(RegisterRequest user, CancellationToken cancellationToken = default);
  Task UpdateAsync(string id, User user);
  Task DeleteAsync(string id);

  /// <summary>Drops cached <see cref="GetByIdAsync"/> entry after persistence updates elsewhere.</summary>
  void ClearCachedUser(string userId);
}
