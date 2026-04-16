using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RhemaBibleAppServerless.Application.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.Mongo;

public sealed class MongoDbService(IOptions<MongoDbSettings> options) : IMongoDbService
{
  private readonly MongoDbSettings _settings = options.Value;
  private readonly IMongoDatabase _database = new MongoClient(options.Value.ConnectionString)
    .GetDatabase(options.Value.DatabaseName);

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