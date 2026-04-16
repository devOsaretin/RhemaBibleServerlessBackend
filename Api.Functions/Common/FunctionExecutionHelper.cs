using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public static class FunctionExecutionHelper
{
  public static async Task<HttpResponseData> ExecuteAsync(
    HttpRequestData req,
    Func<CancellationToken, Task<HttpResponseData>> action,
    CancellationToken cancellationToken,
    ILogger logger,
    IHostEnvironment env)
  {
    try
    {
      return await action(cancellationToken);
    }
    catch (UnauthorizedAccessException ex)
    {
      logger.LogWarning(ex, ex.Message);
      return ErrorResult(req, ex.Message, HttpStatusCode.Unauthorized, ex, env);
    }
    catch (SecurityTokenExpiredException ex)
    {
      logger.LogWarning(ex, "JWT expired");
      return ErrorResult(req, "Token is expired", HttpStatusCode.Unauthorized, ex, env);
    }
    catch (SecurityTokenValidationException ex)
    {
      logger.LogWarning(ex, "JWT validation failed");
      return ErrorResult(req, "Invalid token", HttpStatusCode.Unauthorized, ex, env);
    }
    catch (JsonException ex)
    {
      logger.LogWarning(ex, "Invalid JSON request body");
      return ErrorResult(req, "Invalid request body", HttpStatusCode.BadRequest, ex, env);
    }
    catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Request body is required.", StringComparison.Ordinal))
    {
      logger.LogWarning(ex, "Missing JSON request body");
      return ErrorResult(req, ex.Message, HttpStatusCode.BadRequest, ex, env);
    }
    catch (UserNotFoundException ex)
    {
      logger.LogWarning(ex, ex.Message);
      return ErrorResult(req, ex.Message, HttpStatusCode.NotFound, ex, env);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, ex.Message);
      return ErrorResult(req, "An unexpected error occurred", HttpStatusCode.InternalServerError, ex, env);
    }
  }

  private static HttpResponseData ErrorResult(HttpRequestData req, string message, HttpStatusCode statusCode, Exception ex, IHostEnvironment env)
  {
    var response = env.IsDevelopment()
      ? new ApiException((int)statusCode, message == "An unexpected error occurred" ? ex.Message : message, ex.StackTrace)
      : new ApiException((int)statusCode, message, null);

    return req.CreateJsonResponse(statusCode, response);
  }
}

