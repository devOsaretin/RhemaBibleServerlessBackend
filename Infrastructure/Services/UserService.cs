using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

public class UserService(IMongoDbService mongoDbService, IPasswordHasher passwordHasher, IMemoryCache memoryCache) : IUserService
{
  private readonly IMongoCollection<User> _users = mongoDbService.Users;
  private const string UserCacheKeyPrefix = "user:v1:";

  public async Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken)
  {
    return await _users.Find(_ => true).ToListAsync(cancellationToken);
  }
  public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken)
  {
    var key = UserCacheKeyPrefix + id;
    if (memoryCache.TryGetValue(key, out User? cached))
      return cached;

    var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync(cancellationToken);
    if (user != null)
    {
      memoryCache.Set(
          key,
          user,
          new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45) });
    }

    return user;
  }

  public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
  {
    return await _users.Find(u => u.Email == email).FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<User> FindOrCreateAsync(RegisterRequest user, CancellationToken cancellationToken)
  {
    var existingUser = await _users.Find(u => u.Email == user.Email).FirstOrDefaultAsync(cancellationToken);

    if (existingUser != null)
    {
      return existingUser;
    }

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

    await _users.InsertOneAsync(newUser, new InsertOneOptions(), cancellationToken);
    return newUser;
  }

  public async Task UpdateAsync(string id, User user)
  {
    await _users.ReplaceOneAsync(u => u.Id == id, user);
    memoryCache.Remove(UserCacheKeyPrefix + id);
  }

  public async Task DeleteAsync(string id)
  {
    await _users.DeleteOneAsync(u => u.Id == id);
    memoryCache.Remove(UserCacheKeyPrefix + id);
  }

  public void ClearCachedUser(string userId) =>
      memoryCache.Remove(UserCacheKeyPrefix + userId);
}

