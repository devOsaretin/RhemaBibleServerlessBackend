using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhemaBibleAppServerless.Domain.Models;

public class NoteFunctions(INoteService noteService, IFunctionTokenValidator tokenValidator, IHostEnvironment env, ILogger<NoteFunctions> logger)
{
  [Function("Note_GetNotes")]
  public Task<IActionResult> GetNotes(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/note")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      var pageNumber = int.TryParse(req.Query["pageNumber"], out var parsedPageNumber) ? parsedPageNumber : 0;
      var pageSize = int.TryParse(req.Query["pageSize"], out var parsedPageSize) ? parsedPageSize : 0;

      var pagedResult = await noteService.GetNotesAsync(userId, pageNumber, pageSize);
      var response = ApiResponse<List<Note>>.FromPagedResult(pagedResult);
      return req.ApiResult(response);
    }, cancellationToken, logger, env);

  [Function("Note_CreateNote")]
  public Task<IActionResult> CreateNote(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/note")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var note = await req.ReadRequiredJsonAsync<CreateNoteDto>(ct);

      var newNote = new Note
      {
        AuthId = userId,
        Reference = note.Reference,
        Text = note.Text
      };

      var created = await noteService.CreateNewNoteAsync(newNote);
      return new CreatedResult($"/api/v1/note/{created.Id}", ApiResponse<Note>.SuccessResponse(created));
    }, cancellationToken, logger, env);

  [Function("Note_GetNoteById")]
  public Task<IActionResult> GetNoteById(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/note/{id}")] HttpRequest req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      req.RequireLocalJwtUser(tokenValidator);
      var note = await noteService.GetNoteAsync(id);
      return req.ApiResult(ApiResponse<Note>.SuccessResponse(note));
    }, cancellationToken, logger, env);

  [Function("Note_UpdateNote")]
  public Task<IActionResult> UpdateNote(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/note/{id}")] HttpRequest req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var updatedNote = await req.ReadRequiredJsonAsync<UpdateNoteDto>(ct);

      var note = await noteService.GetNoteAsync(id);
      if (note == null)
        return req.ApiResult(ApiResponse<string>.ErrorResponse("Note not found or you don't have permission"), HttpStatusCode.NotFound);

      var update = new Note
      {
        Id = note.Id,
        AuthId = note.AuthId,
        Text = updatedNote.Text,
        Reference = updatedNote.Reference,
        UpdatedAt = DateTime.UtcNow
      };

      var updated = await noteService.UpdateNoteAsync(id, userId, update);
      if (updated == null)
        return req.ApiResult(ApiResponse<string>.ErrorResponse("Note not found or you don't have permission"), HttpStatusCode.NotFound);

      return req.ApiResult(ApiResponse<Note>.SuccessResponse(updated));
    }, cancellationToken, logger, env);

  [Function("Note_DeleteNote")]
  public Task<IActionResult> DeleteNote(
    [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/note/{id}")] HttpRequest req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      var principal = req.RequireLocalJwtUser(tokenValidator);
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      if (await noteService.DeleteNoteAsync(userId, id))
        return new OkResult();

      return new BadRequestObjectResult("Cannot delete note");
    }, cancellationToken, logger, env);
}

