public class ApiException(int statusCode, string message, string? details, string? correlationId = null)
{
  public int StatusCode { get; set; } = statusCode;
  public string Message { get; set; } = message;

  public string? Details { get; set; } = details;

  /// <summary>
  /// Azure Functions invocation id (or similar) for support correlation; safe to expose to clients.
  /// </summary>
  public string? CorrelationId { get; set; } = correlationId;
}