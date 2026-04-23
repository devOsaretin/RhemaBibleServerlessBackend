using System.Security.Claims;
using RhemaBibleAppServerless.Domain.Enums;

/// <summary>
/// Builds a <see cref="UserDto"/> from Clerk JWT claims when the admin has no corresponding app database row.
/// </summary>
public static class ClerkPrincipalUserDtoMapper
{
  public static UserDto ToClerkOnlyAdminUserDto(this ClaimsPrincipal principal, string clerkUserId)
  {
    var email =
      principal.FindFirst(ClaimTypes.Email)?.Value
      ?? principal.FindFirst("email")?.Value
      ?? string.Empty;

    return new UserDto(
      clerkUserId,
      email,
      principal.FindFirst(ClaimTypes.GivenName)?.Value,
      principal.FindFirst(ClaimTypes.Surname)?.Value,
      SubscriptionType.Free,
      AccountStatus.Active,
      principal.FindFirst("picture")?.Value,
      DateTime.UtcNow,
      !string.IsNullOrWhiteSpace(email),
      null,
      null);
  }
}
