
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


public class Note
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("authId")]
    [Required(ErrorMessage = "User AuthId is required")]
    public required string AuthId { get; set; }

    [BsonElement("reference")]
    [Required(ErrorMessage = "Reference is required")]
    public required string Reference { get; set; }

    [BsonElement("text")]
    [Required(ErrorMessage = "Text is required")]
    public required string Text { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
