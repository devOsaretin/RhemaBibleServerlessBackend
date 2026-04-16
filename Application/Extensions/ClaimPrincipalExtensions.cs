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
}
