using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RhemaBibleAppServerless.Domain.Enums;

namespace RhemaBibleAppServerless.Domain.Models;

public class OtpCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("otpType")]
    public OtpType Type { get; set; }

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("isUsed")]
    public bool IsUsed { get; set; } = false;

    [BsonElement("usedAt")]
    public DateTime? UsedAt { get; set; }

    [BsonElement("attempts")]
    public int Attempts { get; set; } = 0;

    [BsonElement("email")]
    public required string Email { get; set; }

    [BsonElement("isValid")]
    public bool IsValid => !IsUsed && ExpiresAt > DateTime.UtcNow && Attempts < 5;
}
