using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
public class AiFunctions(IAIClient aiClient, IAiQuotaService aiQuotaService, IFunctionTokenValidator tokenValidator)
{
  private static readonly JsonSerializerOptions StreamJsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  [Function("AI_QueryAiService")]
  public async Task<IActionResult> QueryAiService(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/query-ai-service")] HttpRequest req,
    CancellationToken cancellationToken)
  {
    try
    {
      req.RequireLocalJwtUser(tokenValidator);
      var query = await req.ReadRequiredJsonAsync<ChatRequest>(cancellationToken);
      var result = await aiClient.GenerateAsync(query.Prompt, cancellationToken);
      return new OkObjectResult(new { data = result.Data, aiUsage = result.AiUsage });
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return new ObjectResult(QuotaExceededBody(ex))
      {
        StatusCode = StatusCodes.Status429TooManyRequests
      };
    }
  }

  [Function("AI_Prayer")]
  public async Task<IActionResult> Prayer(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/prayer")] HttpRequest req,
    CancellationToken cancellationToken)
  {
    try
    {
      req.RequireLocalJwtUser(tokenValidator);
      var query = await req.ReadRequiredJsonAsync<ChatRequest>(cancellationToken);
      var result = await aiClient.GeneratePrayerAsync(query.Prompt, cancellationToken);
      return new OkObjectResult(new { data = result.Data, aiUsage = result.AiUsage });
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return new ObjectResult(QuotaExceededBody(ex))
      {
        StatusCode = StatusCodes.Status429TooManyRequests
      };
    }
  }

  [Function("AI_Chat")]
  public async Task<IActionResult> Chat(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/chat")] HttpRequest req,
    CancellationToken cancellationToken)
  {
    try
    {
      req.RequireLocalJwtUser(tokenValidator);
      var query = await req.ReadRequiredJsonAsync<ChatRequest>(cancellationToken);
      var result = await aiClient.GenerateChatAsync(query.Prompt, cancellationToken);
      return new OkObjectResult(new { data = result.Data, aiUsage = result.AiUsage });
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return new ObjectResult(QuotaExceededBody(ex))
      {
        StatusCode = StatusCodes.Status429TooManyRequests
      };
    }
  }

  [Function("AI_QueryAiServiceStream")]
  public async Task QueryAiServiceStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/query-ai-service/stream")] HttpRequest req,
    CancellationToken cancellationToken)
  {
    try
    {
      req.RequireLocalJwtUser(tokenValidator);
      var query = await req.ReadRequiredJsonAsync<ChatRequest>(cancellationToken);
      await WriteSseStreamAsync(req, aiClient.StreamGenerateAsync(query.Prompt, cancellationToken), cancellationToken);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      await WriteQuotaExceededIfPossibleAsync(req, ex, cancellationToken);
    }
  }

  [Function("AI_PrayerStream")]
  public async Task PrayerStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/prayer/stream")] HttpRequest req,
    CancellationToken cancellationToken)
  {
    try
    {
      req.RequireLocalJwtUser(tokenValidator);
      var query = await req.ReadRequiredJsonAsync<ChatRequest>(cancellationToken);
      await WriteSseStreamAsync(req, aiClient.StreamGeneratePrayerAsync(query.Prompt, cancellationToken), cancellationToken);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      await WriteQuotaExceededIfPossibleAsync(req, ex, cancellationToken);
    }
  }

  [Function("AI_ChatStream")]
  public async Task ChatStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/chat/stream")] HttpRequest req,
    CancellationToken cancellationToken)
  {
    try
    {
      req.RequireLocalJwtUser(tokenValidator);
      var query = await req.ReadRequiredJsonAsync<ChatRequest>(cancellationToken);
      await WriteSseStreamAsync(req, aiClient.StreamGenerateChatAsync(query.Prompt, cancellationToken), cancellationToken);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      await WriteQuotaExceededIfPossibleAsync(req, ex, cancellationToken);
    }
  }

  private static async Task WriteSseStreamAsync(HttpRequest req, IAsyncEnumerable<AiStreamPart> parts, CancellationToken cancellationToken)
  {
    var response = req.HttpContext.Response;
    response.ContentType = "text/event-stream; charset=utf-8";
    response.Headers.CacheControl = "no-cache";
    response.Headers.Connection = "keep-alive";
    response.Headers.Append("X-Accel-Buffering", "no");

    await foreach (var part in parts.WithCancellation(cancellationToken))
    {
      var payloadJson = part switch
      {
        AiStreamUsagePart u => JsonSerializer.Serialize(new { aiUsage = u.AiUsage }, StreamJsonOptions),
        AiStreamDeltaPart d => JsonSerializer.Serialize(new { delta = d.Delta }, StreamJsonOptions),
        AiStreamDonePart => JsonSerializer.Serialize(new { done = true }, StreamJsonOptions),
        _ => throw new InvalidOperationException($"Unknown stream part: {part.GetType().Name}")
      };

      await response.WriteAsync($"data: {payloadJson}\n\n", cancellationToken);
      await response.Body.FlushAsync(cancellationToken);
    }
  }

  private async Task WriteQuotaExceededIfPossibleAsync(HttpRequest req, AiMonthlyQuotaExceededException ex, CancellationToken cancellationToken)
  {
    var response = req.HttpContext.Response;
    if (response.HasStarted)
      throw ex;

    response.ContentType = "application/json; charset=utf-8";
    response.StatusCode = StatusCodes.Status429TooManyRequests;
    await response.WriteAsJsonAsync(QuotaExceededBody(ex), StreamJsonOptions, cancellationToken);
  }

  private object QuotaExceededBody(AiMonthlyQuotaExceededException ex) => new
  {
    message = ex.Message,
    requiresSubscription = true,
    aiUsage = new AiUsageDto
    {
      IsUnlimited = false,
      FreeCallsRemainingThisMonth = 0,
      FreeCallsLimitPerMonth = aiQuotaService.FreeCallsPerMonth,
      FreeCallsUsedThisMonth = aiQuotaService.FreeCallsPerMonth,
      MonthKeyUtc = AiQuotaService.GetUtcMonthKey(DateTime.UtcNow)
    }
  };
}

