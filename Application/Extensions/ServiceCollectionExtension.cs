using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

public static class ClerkAuthenticationExtensions
{
  public const string ClerkJwtAuthenticationScheme = "Clerk";

  public static IServiceCollection AddCustomJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
  {
    services.Configure<ClerkSettings>(configuration.GetSection("Clerk"));
    services.AddSingleton<IFunctionTokenValidator>(_ => FunctionTokenValidator.Create(configuration));
    return services;
  }
}

public interface IFunctionTokenValidator
{
  ClaimsPrincipal ValidateLocalJwt(string token);
  Task<ClaimsPrincipal> ValidateClerkJwtAsync(string token, CancellationToken cancellationToken = default);
}

internal sealed class FunctionTokenValidator : IFunctionTokenValidator
{
  private readonly JwtSecurityTokenHandler _handler = new();
  private readonly TokenValidationParameters _localJwtValidationParameters;
  private readonly ConfigurationManager<OpenIdConnectConfiguration>? _clerkConfigurationManager;
  private readonly string? _clerkIssuer;

  private FunctionTokenValidator(
    TokenValidationParameters localJwtValidationParameters,
    ConfigurationManager<OpenIdConnectConfiguration>? clerkConfigurationManager,
    string? clerkIssuer)
  {
    _localJwtValidationParameters = localJwtValidationParameters;
    _clerkConfigurationManager = clerkConfigurationManager;
    _clerkIssuer = clerkIssuer;
  }

  public static FunctionTokenValidator Create(IConfiguration configuration)
  {
    var jwtSecretKey = configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("JWT SecretKey is not configured");
    var jwtIssuer = configuration["Jwt:Issuer"] ?? "RhemaApp";
    var jwtAudience = configuration["Jwt:Audience"] ?? "RhemaApp";
    var clerkIssuer = configuration.GetSection("Clerk").Get<ClerkSettings>()?.JwtIssuer?.Trim().TrimEnd('/');

    ConfigurationManager<OpenIdConnectConfiguration>? clerkConfigurationManager = null;
    if (!string.IsNullOrWhiteSpace(clerkIssuer))
    {
      var metadataAddress = $"{clerkIssuer}/.well-known/openid-configuration";
      clerkConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
        metadataAddress,
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever { RequireHttps = metadataAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase) });
    }

    return new FunctionTokenValidator(new TokenValidationParameters
    {
      ValidateIssuer = true,
      ValidateAudience = true,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,
      ValidIssuer = jwtIssuer,
      ValidAudience = jwtAudience,
      IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
      ClockSkew = TimeSpan.Zero,
      RoleClaimType = ClaimTypes.Role,
      NameClaimType = ClaimTypes.NameIdentifier
    }, clerkConfigurationManager, clerkIssuer);
  }

  public ClaimsPrincipal ValidateLocalJwt(string token)
  {
    var principal = _handler.ValidateToken(token, _localJwtValidationParameters, out _);
    NormalizeClaims(principal);
    return principal;
  }

  public async Task<ClaimsPrincipal> ValidateClerkJwtAsync(string token, CancellationToken cancellationToken = default)
  {
    if (_clerkConfigurationManager == null || string.IsNullOrWhiteSpace(_clerkIssuer))
      throw new InvalidOperationException("Clerk JWT validation is not configured.");

    var configuration = await _clerkConfigurationManager.GetConfigurationAsync(cancellationToken);
    var validationParameters = new TokenValidationParameters
    {
      ValidateIssuer = true,
      ValidIssuer = _clerkIssuer,
      ValidateAudience = false,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,
      IssuerSigningKeys = configuration.SigningKeys,
      ClockSkew = TimeSpan.Zero,
      NameClaimType = "email",
      RoleClaimType = ClaimTypes.Role
    };

    var principal = _handler.ValidateToken(token, validationParameters, out _);
    NormalizeClaims(principal);
    return principal;
  }

  private static void NormalizeClaims(ClaimsPrincipal principal)
  {
    var identity = principal.Identity as ClaimsIdentity;
    if (identity == null)
      return;

    var firstName = identity.FindFirst("first_name");
    if (firstName != null)
    {
      identity.RemoveClaim(firstName);
      identity.AddClaim(new Claim(ClaimTypes.GivenName, CapitalizeHelper.Capitalize(firstName.Value)));
    }

    var lastName = identity.FindFirst("last_name");
    if (lastName != null)
    {
      identity.RemoveClaim(lastName);
      identity.AddClaim(new Claim(ClaimTypes.Surname, CapitalizeHelper.Capitalize(lastName.Value)));
    }

    var publicMetadataClaim = identity.FindFirst("publicMetadata")?.Value
        ?? identity.FindFirst("public_metadata")?.Value;

    if (string.IsNullOrEmpty(publicMetadataClaim))
      return;

    try
    {
      using var doc = JsonDocument.Parse(publicMetadataClaim);
      if (doc.RootElement.TryGetProperty("role", out var roleElement))
      {
        var role = roleElement.GetString();
        if (!string.IsNullOrEmpty(role) && !identity.HasClaim(c => c.Type == ClaimTypes.Role))
          identity.AddClaim(new Claim(ClaimTypes.Role, role));
      }
    }
    catch (JsonException)
    {
    }
  }
}
