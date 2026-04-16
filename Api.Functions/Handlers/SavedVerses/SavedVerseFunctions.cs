using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Models;

public class SavedVerseFunctions(ISavedVerseService savedVerseService, IFunctionTokenValidator tokenValidator, IHostEnvironment env, ILogger<SavedVerseFunctions> logger)
{
  [Function("SavedVerse_GetSavedVerses")]
  public Task<IActionResult> GetSavedVerses(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/savedverse")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      var verses = await savedVerseService.GetSavedVersesAsync(userId);
      return req.ApiResult(ApiResponse<IReadOnlyList<SavedVerse>>.SuccessResponse(verses));
    }, cancellationToken, logger, env);

  [Function("SavedVerse_AddVerse")]
  public Task<IActionResult> AddVerse(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/savedverse")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var savedVerseDto = await req.ReadRequiredJsonAsync<SavedVerseDto>(ct);

      var result = await savedVerseService.AddVerseAsync(savedVerseDto, userId);
      return req.ApiResult(ApiResponse<SavedVerse>.SuccessResponse(result));
    }, cancellationToken, logger, env);

  [Function("SavedVerse_DeleteSavedVerse")]
  public Task<IActionResult> DeleteSavedVerse(
    [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/savedverse/{id}")] HttpRequest req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      try
      {
        var deleted = await savedVerseService.DeleteSavedVerseAsync(id, userId);
        if (!deleted)
          return req.ApiResult(ApiResponse<string>.ErrorResponse("Saved verse not found"), HttpStatusCode.NotFound);

        return req.ApiResult(ApiResponse<string>.SuccessResponse("Saved verse deleted successfully"));
      }
      catch (Exception ex)
      {
        return req.ApiResult(ApiResponse<string>.ErrorResponse($"An error occurred: {ex.Message}"), HttpStatusCode.InternalServerError);
      }
    }, cancellationToken, logger, env);
}

