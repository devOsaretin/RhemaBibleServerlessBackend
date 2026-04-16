public class MongoDbSettings
{

  public string ConnectionString { get; set; } = string.Empty;
  public string DatabaseName { get; set; } = string.Empty;

  public string UsersCollectionName { get; set; } = "users";
  public string SavedVersesCollectionName { get; set; } = "savedVerses";
  public string NotesCollectionName { get; set; } = "notes";

  public string RecentActivitiesCollectionName { get; set; } = "recentActivities";

  public string OtpCodesCollectionName { get; set; } = "otpCodes";

  public string ProcessedWebhook { get; set; } = "processedWebhooks";
}