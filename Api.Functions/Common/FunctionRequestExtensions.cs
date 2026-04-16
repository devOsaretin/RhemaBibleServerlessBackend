using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public static class FunctionRequestExtensions
{
  public static async Task<T> ReadRequiredJsonAsync<T>(this HttpRequest request, CancellationToken cancellationToken)
  {
    var value = await request.ReadFromJsonAsync<T>(cancellationToken);
    return value ?? throw new InvalidOperationException("Request body is required.");
  }

  public static ClaimsPrincipal RequireLocalJwtUser(this HttpRequest request, IFunctionTokenValidator tokenValidator)
  {
    var header = request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("Authorization header is missing or invalid.");

    var token = header["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
      throw new UnauthorizedAccessException("Bearer token is missing.");

    var principal = tokenValidator.ValidateLocalJwt(token);
    request.HttpContext.User = principal;
    return principal;
  }

  public static async Task<ClaimsPrincipal> RequireAdminClerkUserAsync(this HttpRequest request, IFunctionTokenValidator tokenValidator, CancellationToken cancellationToken)
  {
    var header = request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("Authorization header is missing or invalid.");

    var token = header["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
      throw new UnauthorizedAccessException("Bearer token is missing.");

    var principal = await tokenValidator.ValidateClerkJwtAsync(token, cancellationToken);
    request.HttpContext.User = principal;

    var role = principal.FindFirst(ClaimTypes.Role)?.Value;
    if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("Admin role is required.");

    return principal;
  }

  public static IActionResult ApiResult<T>(this HttpRequest _, ApiResponse<T> response, HttpStatusCode statusCode = HttpStatusCode.OK) =>
    new ObjectResult(response) { StatusCode = (int)statusCode };
}

