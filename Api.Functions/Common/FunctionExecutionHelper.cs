using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

public static class FunctionExecutionHelper
{
  private const string GenericServerMessage = "An unexpected error occurred";

  public static Task<HttpResponseData> ExecuteAsync(
    HttpRequestData req,
    Func<CancellationToken, Task<HttpResponseData>> action,
    CancellationToken cancellationToken,
    ILogger logger,
    IHostEnvironment env) =>
    ExecuteAsync(req, action, cancellationToken, logger, env, returnHttp200OnUnhandledException: false);

  /// <param name="returnHttp200OnUnhandledException">
  /// When true, any uncaught exception is logged and HTTP 200 is returned (e.g. third-party webhooks that must not see 5xx).
  /// </param>
  public static async Task<HttpResponseData> ExecuteAsync(
    HttpRequestData req,
    Func<CancellationToken, Task<HttpResponseData>> action,
    CancellationToken cancellationToken,
    ILogger logger,
    IHostEnvironment env,
    bool returnHttp200OnUnhandledException)
  {
    try
    {
      return await action(cancellationToken);
    }
    catch (Exception ex)
    {
      if (returnHttp200OnUnhandledException)
      {
        logger.LogError(
          ex,
          "HTTP function failed; returning 200 for webhook compatibility. FunctionName={FunctionName}, InvocationId={InvocationId}",
          InvocationAmbientContext.FunctionName ?? "(unknown)",
          InvocationAmbientContext.InvocationId ?? "(unknown)");
        return req.CreateResponse(HttpStatusCode.OK);
      }

      return await HandleExceptionAsync(req, ex, logger, env);
    }
  }

  /// <summary>
  /// Non-HTTP triggers: log full exception with invocation context, then rethrow (preserves Service Bus / timer retry semantics).
  /// </summary>
  public static async Task ExecuteNonHttpAsync(ILogger logger, CancellationToken cancellationToken, Func<CancellationToken, Task> action)
  {
    try
    {
      await action(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(
        ex,
        "Non-HTTP function failed. FunctionName={FunctionName}, InvocationId={InvocationId}",
        InvocationAmbientContext.FunctionName ?? "(unknown)",
        InvocationAmbientContext.InvocationId ?? "(unknown)");
      throw;
    }
  }

  public static async Task<HttpResponseData> ExecuteWithAuthAsync(
    HttpRequestData req,
    Func<ClaimsPrincipal, CancellationToken, Task<HttpResponseData>> action,
    IFunctionTokenValidator tokenValidator,
    ICurrentPrincipalAccessor principalAccessor,
    IUserApplicationService userService,
    CancellationToken cancellationToken,
    ILogger logger,
    IHostEnvironment env)
  {
    try
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      var userId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrWhiteSpace(userId))
        throw new UnauthorizedAccessException("User id not found in token");

      // IMPORTANT: this is how we revoke access for soft-deleted accounts.
      var user = await userService.GetByIdAsync(userId, cancellationToken);
      if (user == null)
        throw new UnauthorizedAccessException("Account is deleted or unavailable");

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
        logger.LogWarning(unauthorized, "HTTP 401: {Message}", unauthorized.Message);
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
        logger.LogWarning(conflict, "HTTP 409: {Message}", conflict.Message);
        return ErrorResult(req, conflict.Message, HttpStatusCode.Conflict, conflict, env);

      case ForbiddenException forbidden:
        logger.LogWarning(forbidden, "HTTP 403: {Message}", forbidden.Message);
        return ErrorResult(req, forbidden.Message, HttpStatusCode.Forbidden, forbidden, env);

      case BadRequestException badRequest:
        logger.LogWarning(badRequest, "HTTP 400: {Message}", badRequest.Message);
        return ErrorResult(req, badRequest.Message, HttpStatusCode.BadRequest, badRequest, env);

      case ResourceNotFoundException resourceNotFound:
        logger.LogWarning(resourceNotFound, "HTTP 404: {Message}", resourceNotFound.Message);
        return ErrorResult(req, resourceNotFound.Message, HttpStatusCode.NotFound, resourceNotFound, env);

      case UserNotFoundException userNotFound:
        logger.LogWarning(userNotFound, "HTTP 404: {Message}", userNotFound.Message);
        return ErrorResult(req, userNotFound.Message, HttpStatusCode.NotFound, userNotFound, env);

      case AiMonthlyQuotaExceededException quotaExceeded:
        logger.LogWarning(quotaExceeded, "HTTP 429: {Message}", quotaExceeded.Message);
        return ErrorResult(req, quotaExceeded.Message, HttpStatusCode.TooManyRequests, quotaExceeded, env);

      case ArgumentException argumentException when IsUnauthorizedClaimError(argumentException):
        logger.LogWarning(argumentException, "HTTP 401 (claim): {Message}", argumentException.Message);
        return ErrorResult(req, "Invalid token", HttpStatusCode.Unauthorized, argumentException, env);

      case ArgumentException argumentException:
        logger.LogWarning(argumentException, "HTTP 400: {Message}", argumentException.Message);
        return ErrorResult(req, argumentException.Message, HttpStatusCode.BadRequest, argumentException, env);

      default:
        LogUnhandledException(logger, ex);
        return ErrorResult(req, GenericServerMessage, HttpStatusCode.InternalServerError, ex, env);
    }
  }

  private static void LogUnhandledException(ILogger logger, Exception ex)
  {
    logger.LogError(
      ex,
      "Unhandled exception in HTTP function. FunctionName={FunctionName}, InvocationId={InvocationId}",
      InvocationAmbientContext.FunctionName ?? "(unknown)",
      InvocationAmbientContext.InvocationId ?? "(unknown)");
  }

  private static bool IsUnauthorizedClaimError(ArgumentException ex) =>
    ex.Message.StartsWith("Claim '", StringComparison.Ordinal);

  private static async Task<HttpResponseData> ErrorResult(HttpRequestData req, string message, HttpStatusCode statusCode, Exception ex, IHostEnvironment env)
  {
    var correlationId = InvocationAmbientContext.InvocationId;
    var response = env.IsDevelopment()
      ? new ApiException(
          (int)statusCode,
          message == GenericServerMessage ? ex.Message : message,
          ex.StackTrace,
          correlationId)
      : new ApiException((int)statusCode, message, null, correlationId);

    return await req.CreateJsonResponse(statusCode, response);
  }
}

