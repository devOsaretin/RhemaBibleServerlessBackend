using System.Net;
using System.Security.Claims;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;

public static class FunctionRequestDataExtensions
{
  private static readonly JsonSerializerOptions DefaultJsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() }
  };

  public static async Task<T> ReadRequiredJsonAsync<T>(this HttpRequestData request, CancellationToken cancellationToken)
  {
    try
    {
      var value = await JsonSerializer.DeserializeAsync<T>(request.Body, DefaultJsonOptions, cancellationToken);
      return value ?? throw new InvalidOperationException("Request body is required.");
    }
    catch (JsonException ex)
    {
      throw new JsonException("Invalid request body", ex);
    }
  }

  public static ClaimsPrincipal RequireLocalJwtUser(
    this HttpRequestData request,
    IFunctionTokenValidator tokenValidator,
    ICurrentPrincipalAccessor principalAccessor)
  {
    if (!request.Headers.TryGetValues("Authorization", out var values))
      throw new UnauthorizedAccessException("Authorization header is missing or invalid.");

    var header = values.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("Authorization header is missing or invalid.");

    var token = header["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
      throw new UnauthorizedAccessException("Bearer token is missing.");

    var principal = tokenValidator.ValidateLocalJwt(token);
    principalAccessor.Principal = principal;
    return principal;
  }

  public static async Task<ClaimsPrincipal> RequireAdminClerkUserAsync(
    this HttpRequestData request,
    IFunctionTokenValidator tokenValidator,
    ICurrentPrincipalAccessor principalAccessor,
    CancellationToken cancellationToken)
  {
    if (!request.Headers.TryGetValues("Authorization", out var values))
      throw new UnauthorizedAccessException("Authorization header is missing or invalid.");

    var header = values.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("Authorization header is missing or invalid.");

    var token = header["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
      throw new UnauthorizedAccessException("Bearer token is missing.");

    var principal = await tokenValidator.ValidateClerkJwtAsync(token, cancellationToken);
    principalAccessor.Principal = principal;

    var role = principal.FindFirst(ClaimTypes.Role)?.Value;
    if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
      throw new UnauthorizedAccessException("Admin role is required.");

    return principal;
  }

  public static HttpResponseData CreateJsonResponse(this HttpRequestData request, HttpStatusCode statusCode, object body)
  {
    var res = request.CreateResponse(statusCode);
    res.Headers.Add("Content-Type", "application/json; charset=utf-8");
    res.WriteString(JsonSerializer.Serialize(body, DefaultJsonOptions));
    return res;
  }
}

