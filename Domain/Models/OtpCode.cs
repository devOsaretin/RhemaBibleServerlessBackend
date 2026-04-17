using RhemaBibleAppServerless.Domain.Enums;

namespace RhemaBibleAppServerless.Domain.Models;

public class OtpCode
{
  public string? Id { get; set; }

  public string UserId { get; set; } = string.Empty;

  public string Code { get; set; } = string.Empty;

  public OtpType Type { get; set; }

  public DateTime ExpiresAt { get; set; }

  public bool IsUsed { get; set; }

  public DateTime? UsedAt { get; set; }

  public int Attempts { get; set; }

  public required string Email { get; set; }

  public bool IsValid => !IsUsed && ExpiresAt > DateTime.UtcNow && Attempts < 5;
}
