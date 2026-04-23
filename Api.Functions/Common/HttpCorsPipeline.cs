using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

/// <summary>
/// Adds CORS headers for browser clients (preflight OPTIONS and actual responses).
/// Configure comma-separated origins in <c>Cors:AllowedOrigins</c> (app settings / environment).
/// Use <c>*</c> to allow any origin (sets <c>Access-Control-Allow-Origin: *</c> when no <c>Origin</c> header is sent;
/// otherwise echoes the request <c>Origin</c>).
/// </summary>
internal static class HttpCorsPipeline
{
  private const string CorsSectionKey = "Cors:AllowedOrigins";

  public static FunctionsApplicationBuilder AddHttpCors(this FunctionsApplicationBuilder builder)
  {
    builder.Use(next => async context =>
    {
      HttpRequestData? req;
      try
      {
        req = await context.GetHttpRequestDataAsync();
      }
      catch
      {
        await next(context);
        return;
      }

      if (req is null)
      {
        await next(context);
        return;
      }

      var config = context.InstanceServices.GetService<IConfiguration>();

      if (string.Equals(req.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
      {
        var res = req.CreateResponse(System.Net.HttpStatusCode.NoContent);
        ApplyCorsHeaders(req, res, config);
        context.GetInvocationResult().Value = res;
        return;
      }

      await next(context);

      var response = context.GetHttpResponseData();
      if (response is not null)
        ApplyCorsHeaders(req, response, config);
    });

    return builder;
  }

  private static void ApplyCorsHeaders(HttpRequestData req, HttpResponseData res, IConfiguration? config)
  {
    var allowOrigin = ResolveAllowOrigin(req, config);
    if (allowOrigin is null)
      return;

    if (!res.Headers.Contains(HeaderNames.AccessControlAllowOrigin))
      res.Headers.Add(HeaderNames.AccessControlAllowOrigin, allowOrigin);

    if (!res.Headers.Contains(HeaderNames.AccessControlAllowMethods))
      res.Headers.Add(HeaderNames.AccessControlAllowMethods, "GET,POST,PUT,PATCH,DELETE,OPTIONS");

    if (!res.Headers.Contains(HeaderNames.AccessControlAllowHeaders))
      res.Headers.Add(HeaderNames.AccessControlAllowHeaders, "Authorization,Content-Type,X-Requested-With");

    if (!string.Equals(allowOrigin, "*", StringComparison.Ordinal)
        && !res.Headers.Contains(HeaderNames.Vary))
      res.Headers.Add(HeaderNames.Vary, HeaderNames.Origin);
  }

  /// <returns><c>null</c> means do not emit CORS headers (origin not permitted).</returns>
  private static string? ResolveAllowOrigin(HttpRequestData req, IConfiguration? config)
  {
    var configured = config?[CorsSectionKey]?.Trim();
    var requestOrigin = req.Headers.TryGetValues(HeaderNames.Origin, out var origins)
      ? origins.FirstOrDefault()?.Trim()
      : null;

    if (string.IsNullOrEmpty(configured) || string.Equals(configured, "*", StringComparison.Ordinal))
      return string.IsNullOrEmpty(requestOrigin) ? "*" : requestOrigin;

    var allowed = configured
      .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Where(s => s.Length > 0)
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (allowed.Count == 0)
      return string.IsNullOrEmpty(requestOrigin) ? "*" : requestOrigin;

    if (string.IsNullOrEmpty(requestOrigin))
      return null;

    return allowed.Contains(requestOrigin) ? requestOrigin : null;
  }
}
