

using System.Security.Claims;

public interface IJwtService
{
    string GenerateToken(IEnumerable<Claim> claims, int expirationMinutes = 60);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
