using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class ProcessedWebhook
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("processed_at")]
    public DateTime ProcessedAt { get; set; }
}