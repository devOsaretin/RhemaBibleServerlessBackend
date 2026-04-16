using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

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
    catch (Exception ex)
    {
      return await HandleExceptionAsync(req, ex, logger, env);
    }
  }

  public static async Task<HttpResponseData> ExecuteWithAuthAsync(
    HttpRequestData req,
    Func<ClaimsPrincipal, CancellationToken, Task<HttpResponseData>> action,
    IFunctionTokenValidator tokenValidator,
    ICurrentPrincipalAccessor principalAccessor,
    CancellationToken cancellationToken,
    ILogger logger,
    IHostEnvironment env)
  {
    try
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      return await action(principal, cancellationToken);
    }
    catch (Exception ex)
    {
      return await HandleExceptionAsync(req, ex, logger, env);
    }
  }

  private static Task<HttpResponseData> HandleExceptionAsync(
    HttpRequestData req,
    Exception ex,
    ILogger logger,
    IHostEnvironment env)
  {
    switch (ex)
    {
      case UnauthorizedAccessException unauthorized:
        logger.LogWarning(unauthorized, unauthorized.Message);
        return ErrorResult(req, unauthorized.Message, HttpStatusCode.Unauthorized, unauthorized, env);

      case SecurityTokenExpiredException expiredToken:
        logger.LogWarning(expiredToken, "JWT expired");
        return ErrorResult(req, "Token is expired", HttpStatusCode.Unauthorized, expiredToken, env);

      case SecurityTokenValidationException invalidToken:
        logger.LogWarning(invalidToken, "JWT validation failed");
        return ErrorResult(req, "Invalid token", HttpStatusCode.Unauthorized, invalidToken, env);

      case SecurityTokenMalformedException malformedToken:
        logger.LogWarning(malformedToken, "JWT malformed");
        return ErrorResult(req, "Invalid token", HttpStatusCode.Unauthorized, malformedToken, env);

      case JsonException invalidJson:
        logger.LogWarning(invalidJson, "Invalid JSON request body");
        return ErrorResult(req, "Invalid request body", HttpStatusCode.BadRequest, invalidJson, env);

      case InvalidOperationException missingBody
        when string.Equals(missingBody.Message, "Request body is required.", StringComparison.Ordinal):
        logger.LogWarning(missingBody, "Missing JSON request body");
        return ErrorResult(req, missingBody.Message, HttpStatusCode.BadRequest, missingBody, env);

      case ConflictException conflict:
        logger.LogWarning(conflict, conflict.Message);
        return ErrorResult(req, conflict.Message, HttpStatusCode.Conflict, conflict, env);

      case ForbiddenException forbidden:
        logger.LogWarning(forbidden, forbidden.Message);
        return ErrorResult(req, forbidden.Message, HttpStatusCode.Forbidden, forbidden, env);

      case BadRequestException badRequest:
        logger.LogWarning(badRequest, badRequest.Message);
        return ErrorResult(req, badRequest.Message, HttpStatusCode.BadRequest, badRequest, env);

      case ResourceNotFoundException resourceNotFound:
        logger.LogWarning(resourceNotFound, resourceNotFound.Message);
        return ErrorResult(req, resourceNotFound.Message, HttpStatusCode.NotFound, resourceNotFound, env);

      case UserNotFoundException userNotFound:
        logger.LogWarning(userNotFound, userNotFound.Message);
        return ErrorResult(req, userNotFound.Message, HttpStatusCode.NotFound, userNotFound, env);

      case AiMonthlyQuotaExceededException quotaExceeded:
        logger.LogWarning(quotaExceeded, quotaExceeded.Message);
        return ErrorResult(req, quotaExceeded.Message, HttpStatusCode.TooManyRequests, quotaExceeded, env);

      case ArgumentException argumentException when IsUnauthorizedClaimError(argumentException):
        logger.LogWarning(argumentException, argumentException.Message);
        return ErrorResult(req, "Invalid token", HttpStatusCode.Unauthorized, argumentException, env);

      case ArgumentException argumentException:
        logger.LogWarning(argumentException, argumentException.Message);
        return ErrorResult(req, argumentException.Message, HttpStatusCode.BadRequest, argumentException, env);

      default:
        logger.LogError(ex, ex.Message);
        return ErrorResult(req, "An unexpected error occurred", HttpStatusCode.InternalServerError, ex, env);
    }
  }

  private static bool IsUnauthorizedClaimError(ArgumentException ex) =>
    ex.Message.StartsWith("Claim '", StringComparison.Ordinal);

  private static async Task<HttpResponseData> ErrorResult(HttpRequestData req, string message, HttpStatusCode statusCode, Exception ex, IHostEnvironment env)
  {
    var response = env.IsDevelopment()
      ? new ApiException((int)statusCode, message == "An unexpected error occurred" ? ex.Message : message, ex.StackTrace)
      : new ApiException((int)statusCode, message, null);

    return await req.CreateJsonResponse(statusCode, response);
  }
}

