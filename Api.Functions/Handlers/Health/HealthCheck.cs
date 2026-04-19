using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;

public class HealthCheck(ILogger<HealthCheck> logger, IHostEnvironment env)
{
  [Function("Health")]
  public Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(
      req,
      async _ =>
      {
        var version = typeof(HealthCheck).Assembly.GetName().Version?.ToString() ?? "unknown";

        return await req.CreateJsonResponse(HttpStatusCode.OK, new
        {
          status = "ok",
          utc = DateTime.UtcNow,
          service = "Rhema Serverless Backend",
          version
        });
      },
      cancellationToken,
      logger,
      env);
}
