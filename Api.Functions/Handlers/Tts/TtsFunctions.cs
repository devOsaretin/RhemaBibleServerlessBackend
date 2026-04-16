using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class TtsFunctions(ITextToSpeechService textToSpeechService, IFunctionTokenValidator tokenValidator, IHostEnvironment env, ILogger<TtsFunctions> logger)
{
  [Function("Tts_Synthesize")]
  public Task<IActionResult> Synthesize(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tts/synthesize")] HttpRequest req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteAsync(req, async ct =>
    {
      req.RequireLocalJwtUser(tokenValidator);
      var request = await req.ReadRequiredJsonAsync<TextToSpeechRequest>(ct);
      var result = await textToSpeechService.SynthesizeAsync(request, ct);
      return req.ApiResult(ApiResponse<TextToSpeechResponse>.SuccessResponse(result));
    }, cancellationToken, logger, env);
}

