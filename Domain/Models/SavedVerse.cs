
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;



public class SavedVerse
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

    [BsonElement("verse")]
    [Required(ErrorMessage = "Verse is required")]

    public required int Verse { get; set; }

    [BsonElement("pilcrow")]
    public bool Pilcrow { get; set; } = false;

    [BsonElement("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;
}
