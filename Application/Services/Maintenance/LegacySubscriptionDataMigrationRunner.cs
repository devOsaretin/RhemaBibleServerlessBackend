using MongoDB.Bson;
using MongoDB.Driver;

namespace RhemaBibleAppServerless.Application.Services.Maintenance;

public sealed class LegacySubscriptionDataMigrationRunner(
  IMongoDbService mongoDbService,
  ILogger<LegacySubscriptionDataMigrationRunner> logger)
{
  public async Task RunAsync(CancellationToken cancellationToken = default)
  {
    var coll = mongoDbService.Users.Database.GetCollection<BsonDocument>(
      mongoDbService.Users.CollectionNamespace.CollectionName);

    var proMonthly = await coll.UpdateManyAsync(
      Builders<BsonDocument>.Filter.Eq("subscriptionType", "ProMonthly"),
      Builders<BsonDocument>.Update.Set("subscriptionType", "PremiumMonthly"),
      cancellationToken: cancellationToken);

    var proYearly = await coll.UpdateManyAsync(
      Builders<BsonDocument>.Filter.Eq("subscriptionType", "ProYearly"),
      Builders<BsonDocument>.Update.Set("subscriptionType", "PremiumYearly"),
      cancellationToken: cancellationToken);

    if (proMonthly.ModifiedCount > 0 || proYearly.ModifiedCount > 0)
    {
      logger.LogInformation(
        "Migrated legacy Pro subscriptions: ProMonthly→PremiumMonthly {M}, ProYearly→PremiumYearly {Y}",
        proMonthly.ModifiedCount,
        proYearly.ModifiedCount);
    }

    var hasLegacyTts = Builders<BsonDocument>.Filter.Or(
      Builders<BsonDocument>.Filter.Exists("aiTtsFreeCallsMonthKey", true),
      Builders<BsonDocument>.Filter.Exists("aiTtsFreeCallsUsedInMonth", true));
    var unsetTts = await coll.UpdateManyAsync(
      hasLegacyTts,
      Builders<BsonDocument>.Update.Unset("aiTtsFreeCallsMonthKey").Unset("aiTtsFreeCallsUsedInMonth"),
      cancellationToken: cancellationToken);

    if (unsetTts.ModifiedCount > 0)
      logger.LogInformation("Removed deprecated TTS quota fields from {Count} user documents", unsetTts.ModifiedCount);
  }
}
