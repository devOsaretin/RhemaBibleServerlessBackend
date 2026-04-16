using MongoDB.Driver;

public interface IMongoDbService
{
  IMongoCollection<User> Users { get; }
  IMongoCollection<SavedVerse> SavedVerses { get; }
  IMongoCollection<Note> Notes { get; }
  IMongoCollection<RecentActivity> RecentActivities { get; }
  IMongoCollection<OtpCode> OtpCode { get; }
  IMongoCollection<ProcessedWebhook> ProcessedWebhook { get; }
}

