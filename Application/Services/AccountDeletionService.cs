using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Shared.Helpers;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Application.Configuration;

public sealed class AccountDeletionService(
  IUserPersistence users,
  INoteRepository notes,
  ISavedVerseRepository verses,
  IRecentActivityRepository activities,
  IOtpRepository otps,
  IServiceBusService serviceBusService,
  IUserApplicationService userApplicationService,
  IUserResourceEpochStore epochStore,
  IOptions<AccountDeletionOptions> options) : IAccountDeletionService
{
  private readonly AccountDeletionOptions _options = options.Value;

  public async Task RequestDeletionAsync(string userId, CancellationToken cancellationToken = default)
  {
    var now = DateTime.UtcNow;

    // Grab email before the account becomes inaccessible
    var user = await userApplicationService.GetByIdAsync(userId, cancellationToken);
    var recipient = user?.Email;

    // Mark deleted + revoke refresh token (access tokens are effectively revoked by auth guard)
    await users.MarkDeletedAsync(userId, now, cancellationToken);

    // Clear caches (best-effort)
    userApplicationService.ClearCachedUser(userId);
    epochStore.BumpNotes(userId);
    epochStore.BumpSavedVerses(userId);
    epochStore.BumpRecentActivity(userId);

    // Notify user
    if (!string.IsNullOrWhiteSpace(recipient))
    {
      var subject = "Rhema Bible — Deletion requested";
      var body = EmailTemplates.AccountDeletionRequested(now, Math.Clamp(_options.GraceDays, 0, 30));
      var msg = new EmailRequestFromQueueDto
      {
        Recipient = recipient,
        Subject = subject,
        Body = body
      };
      await serviceBusService.PublishAsync(msg, QueueNames.Email, cancellationToken);
    }
  }

  public async Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
  {
    var graceDays = Math.Clamp(_options.GraceDays, 0, 30);
    var purgeBefore = utcNow.AddDays(-graceDays);

    var due = await users.ListUsersDueForPurgeAsync(purgeBefore, take: 200, cancellationToken);
    var purged = 0;

    foreach (var u in due)
    {
      // Remove dependents, then user record
      await notes.DeleteAllByUserAsync(u.Id!, cancellationToken);
      await verses.DeleteAllByUserAsync(u.Id!, cancellationToken);
      await activities.DeleteAllByUserAsync(u.Id!, cancellationToken);
      await otps.DeleteAllByUserAsync(u.Id!, cancellationToken);
      await users.DeleteAsync(u.Id!, cancellationToken);
      purged++;

      // Optional final notice (best-effort)
      if (!string.IsNullOrWhiteSpace(u.Email))
      {
        var subject = "Rhema Bible — Account deleted";
        var body = EmailTemplates.AccountDeleted(utcNow);
        var msg = new EmailRequestFromQueueDto
        {
          Recipient = u.Email,
          Subject = subject,
          Body = body
        };
        await serviceBusService.PublishAsync(msg, QueueNames.Email, cancellationToken);
      }
    }

    return purged;
  }
}

