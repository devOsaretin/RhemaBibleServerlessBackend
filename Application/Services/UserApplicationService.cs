using Microsoft.Extensions.Caching.Memory;
using RhemaBibleAppServerless.Application.Persistence;

public class UserApplicationService(IUserPersistence users, IPasswordHasher passwordHasher, IMemoryCache memoryCache)
  : IUserApplicationService
{
  private const string UserCacheKeyPrefix = "user:v1:";

  public async Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken) =>
    await users.GetAllAsync(cancellationToken);

  public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken)
  {
    var key = UserCacheKeyPrefix + id;
    if (memoryCache.TryGetValue(key, out User? cached))
      return cached;

    var user = await users.GetByIdAsync(id, cancellationToken);
    if (user != null)
    {
      memoryCache.Set(
        key,
        user,
        new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45) });
    }

    return user;
  }

  public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
    users.GetByEmailAsync(email, cancellationToken);

  public async Task<User> FindOrCreateAsync(RegisterRequest user, CancellationToken cancellationToken)
  {
    var existingUser = await users.GetByEmailAsync(user.Email, cancellationToken);
    if (existingUser != null)
      return existingUser;

    var newUser = new User
    {
      Email = user.Email,
      FirstName = user.FirstName,
      LastName = user.LastName,
      Password = passwordHasher.HashPassword(user.Password),
      SubscriptionType = SubscriptionType.Free,
      CreatedAt = DateTime.UtcNow,
      IsEmailVerified = false,
    };

    await users.InsertAsync(newUser, cancellationToken);
    return newUser;
  }

  public async Task UpdateAsync(string id, User user)
  {
    await users.ReplaceAsync(id, user, CancellationToken.None);
    memoryCache.Remove(UserCacheKeyPrefix + id);
  }

  public async Task DeleteAsync(string id)
  {
    await users.DeleteAsync(id, CancellationToken.None);
    memoryCache.Remove(UserCacheKeyPrefix + id);
  }

  public void ClearCachedUser(string userId) =>
    memoryCache.Remove(UserCacheKeyPrefix + userId);
}
