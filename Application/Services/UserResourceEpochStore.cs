using System.Collections.Concurrent;


public sealed class UserResourceEpochStore : IUserResourceEpochStore
{
    private readonly ConcurrentDictionary<string, long> _notes = new();
    private readonly ConcurrentDictionary<string, long> _verses = new();
    private readonly ConcurrentDictionary<string, long> _activity = new();

    public long GetNotesEpoch(string userId) => Get(_notes, userId);
    public long BumpNotes(string userId) => Bump(_notes, userId);

    public long GetSavedVersesEpoch(string userId) => Get(_verses, userId);
    public long BumpSavedVerses(string userId) => Bump(_verses, userId);

    public long GetRecentActivityEpoch(string userId) => Get(_activity, userId);
    public long BumpRecentActivity(string userId) => Bump(_activity, userId);

    private static long Get(ConcurrentDictionary<string, long> map, string userId) =>
        map.GetValueOrDefault(userId, 0);

    private static long Bump(ConcurrentDictionary<string, long> map, string userId) =>
        map.AddOrUpdate(userId, 1L, (_, v) => v + 1);
}
