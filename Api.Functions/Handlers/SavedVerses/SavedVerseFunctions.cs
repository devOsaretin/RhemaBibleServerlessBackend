using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Models;

public class SavedVerseFunctions(
  ISavedVerseService savedVerseService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  IHostEnvironment env,
  ILogger<SavedVerseFunctions> logger)
{
  [Function("SavedVerse_GetSavedVerses")]
  public Task<HttpResponseData> GetSavedVerses(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/savedverse")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      var verses = await savedVerseService.GetSavedVersesAsync(userId);
      return req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<IReadOnlyList<SavedVerse>>.SuccessResponse(verses));
    }, cancellationToken, logger, env);

  [Function("SavedVerse_AddVerse")]
  public Task<HttpResponseData> AddVerse(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/savedverse")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var savedVerseDto = await req.ReadRequiredJsonAsync<SavedVerseDto>(ct);

      var result = await savedVerseService.AddVerseAsync(savedVerseDto, userId);
      return req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<SavedVerse>.SuccessResponse(result));
    }, cancellationToken, logger, env);

  [Function("SavedVerse_DeleteSavedVerse")]
  public Task<HttpResponseData> DeleteSavedVerse(
    [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/savedverse/{id}")] HttpRequestData req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator, principalAccessor);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      var deleted = await savedVerseService.DeleteSavedVerseAsync(id, userId);
      if (!deleted)
        return req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<string>.ErrorResponse("Saved verse not found"));

      return req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<string>.SuccessResponse("Saved verse deleted successfully"));
    }, cancellationToken, logger, env);
}

