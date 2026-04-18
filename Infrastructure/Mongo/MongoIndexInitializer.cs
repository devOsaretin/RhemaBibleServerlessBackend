using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Configuration;

namespace RhemaBibleAppServerless.Infrastructure.Mongo;

public sealed class MongoIndexInitializer(
  IOptions<MongoDbSettings> mongoSettings,
  IOptions<MongoIndexInitializationOptions> indexOptions,
  ILogger<MongoIndexInitializer> logger)
{
  public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
  {
    if (!indexOptions.Value.EnsureOnStartup)
    {
      logger.LogInformation("Mongo index maintenance skipped (MongoIndexes:EnsureOnStartup=false).");
      return;
    }

    var settings = mongoSettings.Value;
    if (string.IsNullOrWhiteSpace(settings.ConnectionString) || string.IsNullOrWhiteSpace(settings.DatabaseName))
    {
      logger.LogWarning("Mongo index maintenance skipped: connection or database name is empty.");
      return;
    }

    var client = new MongoClient(settings.ConnectionString);
    var database = client.GetDatabase(settings.DatabaseName);

    var usersCollection = database.GetCollection<User>(settings.UsersCollectionName);
    var userIndexKeys = Builders<User>.IndexKeys.Ascending(u => u.Email);
    var userIndexModel = new CreateIndexModel<User>(userIndexKeys, new CreateIndexOptions { Unique = true });
    await usersCollection.Indexes.CreateOneAsync(userIndexModel, cancellationToken: cancellationToken);

    var otpCollection = database.GetCollection<OtpCode>(settings.OtpCodesCollectionName);
    var otpIndexKeys = Builders<OtpCode>.IndexKeys.Ascending(o => o.ExpiresAt);
    var otpIndexModel = new CreateIndexModel<OtpCode>(
      otpIndexKeys,
      new CreateIndexOptions { ExpireAfter = TimeSpan.Zero });
    await otpCollection.Indexes.CreateOneAsync(otpIndexModel, cancellationToken: cancellationToken);

    logger.LogInformation("Mongo indexes ensured (users email unique, OTP TTL).");
  }
}
