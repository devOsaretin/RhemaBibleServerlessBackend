using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class DocsFunctions(ILogger<DocsFunctions> logger, IHostEnvironment env)
{
  private static string BuildSwaggerUiHtml(Uri requestUrl)
  {
    var specUrl =
      $"{requestUrl.GetLeftPart(UriPartial.Authority)}{requestUrl.AbsolutePath.TrimEnd('/')}/openapi.json";
    var specJsLiteral = JsonSerializer.Serialize(specUrl);
    var bootScript =
      $"<script>window.onload = () => {{ window.ui = SwaggerUIBundle({{ url: {specJsLiteral}, dom_id: '#swagger-ui' }}); }};</script>";

    return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Rhema Bible API</title>
  <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui.css" crossorigin="anonymous" />
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui-bundle.js" crossorigin="anonymous"></script>
""" + bootScript + """
</body>
</html>
""";
  }

  [Function("Docs_Ui")]
  public Task<HttpResponseData> DocsUi(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/docs")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(
      req,
      async ct =>
      {
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/html; charset=utf-8");
        res.Headers.Add("Cache-Control", "no-cache");
        await res.WriteStringAsync(BuildSwaggerUiHtml(req.Url), ct);
        return res;
      },
      cancellationToken,
      logger,
      env);

  [Function("Docs_OpenApiJson")]
  public Task<HttpResponseData> OpenApiJson(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/docs/openapi.json")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(
      req,
      async ct =>
      {
        var path = Path.Combine(AppContext.BaseDirectory, "openapi", "v1.json");
        if (!File.Exists(path))
        {
          logger.LogError("OpenAPI spec missing at {Path}", path);
          return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var json = await File.ReadAllTextAsync(path, ct);
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        res.Headers.Add("Cache-Control", "public, max-age=300");
        await res.WriteStringAsync(json, ct);
        return res;
      },
      cancellationToken,
      logger,
      env);
}
