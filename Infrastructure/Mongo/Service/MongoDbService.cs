
using Microsoft.Extensions.Options;
using MongoDB.Driver;


public class MongoDbService
  : IMongoDbService
{
  private readonly IMongoDatabase _database;
  private readonly MongoDbSettings _settings;

  public MongoDbService(IOptions<MongoDbSettings> options)
  {
    _settings = options.Value;
    var client = new MongoClient(_settings.ConnectionString);
    _database = client.GetDatabase(_settings.DatabaseName);

    // Ensure unique index on Email field
    var usersCollection = _database.GetCollection<User>("users");
    var indexKeysDefinition = Builders<User>.IndexKeys.Ascending(u => u.Email);
    var indexOptions = new CreateIndexOptions { Unique = true };
    var indexModel = new CreateIndexModel<User>(indexKeysDefinition, indexOptions);

    usersCollection.Indexes.CreateOne(indexModel);

    var otpCollection = _database.GetCollection<OtpCode>(_settings.OtpCodesCollectionName);

    var otpIndexKeys = Builders<OtpCode>.IndexKeys.Ascending(o => o.ExpiresAt);

    var otpIndexOptions = new CreateIndexOptions
    {
      ExpireAfter = TimeSpan.Zero // Deletes exactly when ExpiresAt is passed
    };

    var otpIndexModel = new CreateIndexModel<OtpCode>(otpIndexKeys, otpIndexOptions);

    otpCollection.Indexes.CreateOne(otpIndexModel);
  }

  public IMongoCollection<User> Users => _database
  .GetCollection<User>(_settings.UsersCollectionName);
  public IMongoCollection<SavedVerse> SavedVerses => _database
  .GetCollection<SavedVerse>(_settings.SavedVersesCollectionName);
  public IMongoCollection<Note> Notes => _database
  .GetCollection<Note>(_settings.NotesCollectionName);
  public IMongoCollection<RecentActivity> RecentActivities => _database
  .GetCollection<RecentActivity>(_settings.RecentActivitiesCollectionName);

  public IMongoCollection<OtpCode> OtpCode => _database.GetCollection<OtpCode>(_settings.OtpCodesCollectionName);

  public IMongoCollection<ProcessedWebhook> ProcessedWebhook => _database.GetCollection<ProcessedWebhook>(_settings.ProcessedWebhook);
}