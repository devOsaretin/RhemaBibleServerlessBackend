using System.Security.Claims;

public static class ClaimsPrincipalExtensions
{
  public static string GetRequiredClaim(this ClaimsPrincipal user, string claimType)
  {
    var value = user.FindFirst(claimType)?.Value;
    if (string.IsNullOrWhiteSpace(value))
      throw new ArgumentException($"Claim '{claimType}' is missing or empty.");
    return value;
  }


  public static string? GetAuthenticatedUserId(this ClaimsPrincipal user) =>
    user.FindFirst(ClaimTypes.NameIdentifier)?.Value
    ?? user.FindFirst("sub")?.Value
    ?? user.FindFirst("nameid")?.Value
    ?? user.FindFirst(ClaimTypes.Name)?.Value
    ?? user.FindFirst("userId")?.Value
    ?? user.FindFirst("user_id")?.Value;
}
