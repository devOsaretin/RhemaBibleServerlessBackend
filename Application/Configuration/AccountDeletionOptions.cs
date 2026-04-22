namespace RhemaBibleAppServerless.Application.Configuration;

public sealed class AccountDeletionOptions
{
  public const string SectionName = "AccountDeletion";

  /// <summary>
  /// Grace window before permanent purge. Keep it within 7-30 days.
  /// </summary>
  public int GraceDays { get; set; } = 30;
}

