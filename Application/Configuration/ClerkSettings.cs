public class ClerkSettings
{
  public string PublishableKey { get; set; } = string.Empty;
  public required string SecretKey { get; set; }
  public required string JwtIssuer { get; set; }

}
