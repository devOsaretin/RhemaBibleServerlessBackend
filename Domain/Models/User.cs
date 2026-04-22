using System.ComponentModel.DataAnnotations;
using RhemaBibleAppServerless.Domain.Enums;

namespace RhemaBibleAppServerless.Domain.Models;

public class User
{
  public string? Id { get; set; }

  [Required(ErrorMessage = "Email is required")]
  public required string Email { get; set; }

  public required string Password { get; set; }

  public string? FirstName { get; set; }

  public string? LastName { get; set; }

  [EnumDataType(typeof(SubscriptionType), ErrorMessage = "Invalid subscription type")]
  public SubscriptionType SubscriptionType { get; set; } = SubscriptionType.Free;

  public string? ImageUrl { get; set; }

  public AccountStatus Status { get; set; } = AccountStatus.Active;

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public DateTime UpdatedAt { get; set; }

  public string RefreshToken { get; set; } = string.Empty;

  public DateTime RefreshTokenExpiryTime { get; set; }

  public bool IsEmailVerified { get; set; }

  public bool IsDeleted { get; set; }

  public DateTime? DeletedAt { get; set; }

  public DateTime? SubscriptionExpiresAt { get; set; }

  public string? AiFreeCallsMonthKey { get; set; }

  public int AiFreeCallsUsedInMonth { get; set; }
}
