using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class TtsFunctions(
  ITextToSpeechService textToSpeechService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  IHostEnvironment env,
  ILogger<TtsFunctions> logger)
{
  [Function("Tts_Synthesize")]
  public Task<HttpResponseData> Synthesize(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tts/synthesize")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(req, async (_, ct) =>
    {
      var request = await req.ReadRequiredJsonAsync<TextToSpeechRequest>(ct);
      var result = await textToSpeechService.SynthesizeAsync(request, ct);
      return await req.CreateJsonResponse(HttpStatusCode.OK, ApiResponse<TextToSpeechResponse>.SuccessResponse(result));
    }, tokenValidator, principalAccessor, cancellationToken, logger, env);
}

