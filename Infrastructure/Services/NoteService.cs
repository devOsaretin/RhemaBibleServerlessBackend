using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

public class NoteService(
    IMongoDbService mongoDbService,
    IRecentActivityService recentActivityService,
    IMemoryCache memoryCache,
    IUserResourceEpochStore epochStore) : INoteService
{
    public async Task<Note> CreateNewNoteAsync(Note note)
    {

        await mongoDbService.Notes.InsertOneAsync(note);
        epochStore.BumpNotes(note.AuthId);

        _ = Task.Run(async () =>
    {
        try
        {
            var activity = new RecentActivity
            {
                AuthId = note.AuthId,
                ActivityType = ActivityType.AddNote,
                Title = $"Added note to {note.Reference}"
            };

            await recentActivityService.AddActivityByUser(activity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Failed to log activity: {ex.Message}");
        }
    });


        return note;
    }

    public async Task<bool> DeleteNoteAsync(string userId, string noteId)
    {
        var result = await mongoDbService.Notes
        .DeleteOneAsync(x => x.AuthId == userId && x.Id == noteId);

        if (result.DeletedCount > 0)
            epochStore.BumpNotes(userId);

        return result.DeletedCount > 0;
    }

    public async Task<Note> GetNoteAsync(string noteId)
    {
        return await mongoDbService.Notes.Find(x => x.Id == noteId).FirstOrDefaultAsync();
    }

    public async Task<PagedResult<Note>> GetNotesAsync(string userId, int pageNumber = 1, int pageSize = 10)
    {
        var epoch = epochStore.GetNotesEpoch(userId);
        var cacheKey = $"notes:v1:{userId}:{epoch}:{pageNumber}:{pageSize}";

        return (await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(1));

            var skip = (pageNumber - 1) * pageSize;

            var filter = Builders<Note>.Filter.Eq(x => x.AuthId, userId);
            var sort = Builders<Note>.Sort.Descending(x => x.CreatedAt);

            var documentCountTask = mongoDbService.Notes.CountDocumentsAsync(filter);
            var itemsTask = mongoDbService.Notes
                .Find(filter)
                .Sort(sort)
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync();
            await Task.WhenAll(documentCountTask, itemsTask);

            return new PagedResult<Note>()
            {
                Items = itemsTask.Result,
                TotalItems = documentCountTask.Result,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }))!;
    }

    public async Task<Note?> UpdateNoteAsync(string noteId, string userId, Note updatedNote)
    {

        var filter = Builders<Note>.Filter.And(
            Builders<Note>.Filter.Eq(n => n.Id, noteId),
            Builders<Note>.Filter.Eq(n => n.AuthId, userId)
        );

        // Define which fields to update
        var update = Builders<Note>.Update
            .Set(n => n.Reference, updatedNote.Reference)
            .Set(n => n.Text, updatedNote.Text)
            .Set(n => n.UpdatedAt, DateTime.UtcNow);

        // Return the updated note (MongoDB feature)
        var options = new FindOneAndUpdateOptions<Note>
        {
            ReturnDocument = ReturnDocument.After // returns updated version, not old one
        };

        // Perform update and return the updated document
        var result = await mongoDbService.Notes.FindOneAndUpdateAsync(filter, update, options);
        if (result != null)
            epochStore.BumpNotes(userId);
        return result;
    }
}

