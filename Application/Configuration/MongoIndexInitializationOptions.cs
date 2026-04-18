namespace RhemaBibleAppServerless.Application.Configuration;

public sealed class MongoIndexInitializationOptions
{
  public const string SectionName = "MongoIndexes";
  public bool EnsureOnStartup { get; set; } = true;
}
