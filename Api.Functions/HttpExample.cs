using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public class HttpExample
{
    [Function("Health")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequest req)
    {
        var version = typeof(HttpExample).Assembly.GetName().Version?.ToString() ?? "unknown";

        return new OkObjectResult(new
        {
            status = "ok",
            utc = DateTime.UtcNow,
            service = "rhemapp-backend",
            version
        });
    }
}
