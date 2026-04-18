using System.Security.Claims;

public interface IFunctionTokenValidator
{
    ClaimsPrincipal ValidateLocalJwt(string token);
    Task<ClaimsPrincipal> ValidateClerkJwtAsync(string token, CancellationToken cancellationToken = default);
}