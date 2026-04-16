using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RhemaBibleAppServerless.Domain.Enums;

namespace RhemaBibleAppServerless.Domain.Models;

public class User
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [BsonElement("email")]
  [Required(ErrorMessage = "Email is required")]
  public required string Email { get; set; }

  [BsonElement("password")]
  public required string Password { get; set; }


  [BsonElement("firstName")]
  public string? FirstName { get; set; }

  [BsonElement("lastName")]
  public string? LastName { get; set; }

  [BsonElement("subscriptionType")]
  [EnumDataType(typeof(SubscriptionType), ErrorMessage = "Invalid subscription type")]

  public SubscriptionType SubscriptionType { get; set; } = SubscriptionType.Free;

  [BsonElement("imageUrl")]
  public string? ImageUrl { get; set; }

  [BsonElement("status")]
  public AccountStatus Status { get; set; } = AccountStatus.Active;


  [BsonElement("createdAt")]
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  [BsonElement("updatedAt")]
  public DateTime UpdatedAt { get; set; }


  [BsonElement("refreshToken")]
  public string RefreshToken { get; set; } = string.Empty;

  [BsonElement("refreshTokenExpiryTime")]
  public DateTime RefreshTokenExpiryTime { get; set; }

  [BsonElement("isEmailVerified")]
  public bool IsEmailVerified { get; set; }

  [BsonElement("subscriptionExpiresAt")]
  public DateTime? SubscriptionExpiresAt { get; set; }

  [BsonElement("aiFreeCallsMonthKey")]
  public string? AiFreeCallsMonthKey { get; set; }

  [BsonElement("aiFreeCallsUsedInMonth")]
  public int AiFreeCallsUsedInMonth { get; set; }

}