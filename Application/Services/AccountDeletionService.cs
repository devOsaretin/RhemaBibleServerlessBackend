using RhemaBibleAppServerless.Application.Persistence;

public sealed class AccountDeletionService(
  IUserPersistence users,
  INoteRepository notes,
  ISavedVerseRepository verses,
  IRecentActivityRepository activities,
  IOtpRepository otps,
  IUserApplicationService userApplicationService,
  IUserResourceEpochStore epochStore) : IAccountDeletionService
{
  public async Task DeleteMyAccountAsync(string userId, CancellationToken cancellationToken = default)
  {
    // Delete dependents first (safe even if already empty)
    await notes.DeleteAllByUserAsync(userId, cancellationToken);
    await verses.DeleteAllByUserAsync(userId, cancellationToken);
    await activities.DeleteAllByUserAsync(userId, cancellationToken);
    await otps.DeleteAllByUserAsync(userId, cancellationToken);

    // Delete user record last
    await users.DeleteAsync(userId, cancellationToken);

    // Clear caches (best-effort)
    userApplicationService.ClearCachedUser(userId);
    epochStore.BumpNotes(userId);
    epochStore.BumpSavedVerses(userId);
    epochStore.BumpRecentActivity(userId);
  }
}

