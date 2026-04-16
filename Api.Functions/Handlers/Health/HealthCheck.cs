using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

public class HealthCheck
{
    [Function("Health")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
    {
        var version = typeof(HealthCheck).Assembly.GetName().Version?.ToString() ?? "unknown";

        return await req.CreateJsonResponse(HttpStatusCode.OK, new
        {
            status = "ok",
            utc = DateTime.UtcNow,
            service = "Rhema Serverless Backend",
            version
        });
    }
}
