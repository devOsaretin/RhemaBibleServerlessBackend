
public interface IUserResourceEpochStore
{
    long GetNotesEpoch(string userId);
    long BumpNotes(string userId);

    long GetSavedVersesEpoch(string userId);
    long BumpSavedVerses(string userId);

    long GetRecentActivityEpoch(string userId);
    long BumpRecentActivity(string userId);
}
