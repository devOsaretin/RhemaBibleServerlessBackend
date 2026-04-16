
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

using RhemaBibleAppServerless.Domain.Enums;

namespace RhemaBibleAppServerless.Domain.Models;



public class RecentActivity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("authId")]
    [Required(ErrorMessage = "User AuthId is required")]
    public required string AuthId { get; set; }

    [BsonElement("title")]
    [Required(ErrorMessage = "Title is required")]
    public required string Title { get; set; }

    [BsonRepresentation(BsonType.String)]
    [Required(ErrorMessage = "Activity type is required")]
    [BsonElement("activityType")]
    [EnumDataType(typeof(ActivityType), ErrorMessage = "Invalid activity type")]
    public required ActivityType ActivityType { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
