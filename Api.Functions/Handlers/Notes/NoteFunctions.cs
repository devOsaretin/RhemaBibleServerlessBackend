using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


public class NoteFunctions(
  INoteService noteService,
  IUserApplicationService userService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  IHostEnvironment env,
  ILogger<NoteFunctions> logger)
{
  [Function("Note_GetNotes")]
  public Task<HttpResponseData> GetNotes(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/note")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      var (pageNumber, pageSize) = req.GetPagination();
      logger.LogInformation("pageNumber: {0}, pageSize: {1}", pageNumber, pageSize);

      var pagedResult = await noteService.GetNotesAsync(userId, pageNumber, pageSize);
      var response = ApiResponse<List<Note>>.FromPagedResult(pagedResult);
      return await req.CreateJsonResponse(HttpStatusCode.OK, response);
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);

  [Function("Note_CreateNote")]
  public Task<HttpResponseData> CreateNote(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/note")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var note = await req.ReadRequiredJsonAsync<CreateNoteDto>(ct);

      var newNote = new Note
      {
        AuthId = userId,
        Reference = note.Reference,
        Text = note.Text
      };

      var created = await noteService.CreateNewNoteAsync(newNote);
      var res = await req.CreateJsonResponse(HttpStatusCode.Created, ApiResponse<Note>.SuccessResponse(created));
      res.Headers.Add("Location", $"/api/v1/note/{created.Id}");
      return res;
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);

  [Function("Note_GetNoteById")]
  public Task<HttpResponseData> GetNoteById(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/note/{id}")] HttpRequestData req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetAuthenticatedUserId();
      if (string.IsNullOrEmpty(userId))
        return await req.CreateJsonResponse(HttpStatusCode.Unauthorized, ApiResponse<Note>.ErrorResponse("User id not found in token"));

      var note = await noteService.GetNoteAsync(id);
      if (note == null)
        return await req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<Note>.ErrorResponse("Note not found"));

      if (!string.Equals(note.AuthId, userId, StringComparison.Ordinal))
        return await req.CreateJsonResponse(HttpStatusCode.Forbidden, ApiResponse<Note>.ErrorResponse("You do not have access to this note."));

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<Note>.SuccessResponse(note));
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);

  [Function("Note_UpdateNote")]
  public Task<HttpResponseData> UpdateNote(
    [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/note/{id}")] HttpRequestData req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);
      var updatedNote = await req.ReadRequiredJsonAsync<UpdateNoteDto>(ct);

      var note = await noteService.GetNoteAsync(id);
      if (note == null)
        return await req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<string>.ErrorResponse("Note not found or you don't have permission"));

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
        return await req.CreateJsonResponse(HttpStatusCode.NotFound, ApiResponse<string>.ErrorResponse("Note not found or you don't have permission"));

      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<Note>.SuccessResponse(updated));
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);

  [Function("Note_DeleteNote")]
  public Task<HttpResponseData> DeleteNote(
    [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/note/{id}")] HttpRequestData req,
    string id,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (principal, ct) =>
    {
      var userId = principal.GetRequiredClaim(ClaimTypes.NameIdentifier);

      if (await noteService.DeleteNoteAsync(userId, id))
        return req.CreateResponse(HttpStatusCode.OK);

      return await req.CreateJsonResponse(HttpStatusCode.BadRequest, new { message = "Cannot delete note" });
    }, tokenValidator, principalAccessor, userService, cancellationToken, logger, env);
}

