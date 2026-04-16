using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class AiFunctions(
  IAIClient aiClient,
  IAiQuotaService aiQuotaService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  ILogger<AiFunctions> logger,
  IHostEnvironment env)
{
  private static readonly JsonSerializerOptions StreamJsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  [Function("AI_QueryAiService")]
  public async Task<HttpResponseData> QueryAiService(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/query-ai-service")] HttpRequestData req,
    CancellationToken cancellationToken)
  {
    try
    {
      return await FunctionExecutionHelper.ExecuteWithAuthAsync(
        req,
        async (_, ct) =>
        {
          var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
          var result = await aiClient.GenerateAsync(query.Prompt, ct);
          return await req.CreateJsonResponse(HttpStatusCode.OK, new { data = result.Data, aiUsage = result.AiUsage });
        },
        tokenValidator,
        principalAccessor,
        cancellationToken,
        logger,
        env);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return await req.CreateJsonResponse(HttpStatusCode.TooManyRequests, QuotaExceededBody(ex));
    }
  }

  [Function("AI_Prayer")]
  public async Task<HttpResponseData> Prayer(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/prayer")] HttpRequestData req,
    CancellationToken cancellationToken)
  {
    try
    {
      return await FunctionExecutionHelper.ExecuteWithAuthAsync(
        req,
        async (_, ct) =>
        {
          var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
          var result = await aiClient.GeneratePrayerAsync(query.Prompt, ct);
          return await req.CreateJsonResponse(HttpStatusCode.OK, new { data = result.Data, aiUsage = result.AiUsage });
        },
        tokenValidator,
        principalAccessor,
        cancellationToken,
        logger,
        env);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return await req.CreateJsonResponse(HttpStatusCode.TooManyRequests, QuotaExceededBody(ex));
    }
  }

  [Function("AI_Chat")]
  public async Task<HttpResponseData> Chat(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/chat")] HttpRequestData req,
    CancellationToken cancellationToken)
  {
    try
    {
      return await FunctionExecutionHelper.ExecuteWithAuthAsync(
        req,
        async (_, ct) =>
        {
          var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
          var result = await aiClient.GenerateChatAsync(query.Prompt, ct);
          return await req.CreateJsonResponse(HttpStatusCode.OK, new { data = result.Data, aiUsage = result.AiUsage });
        },
        tokenValidator,
        principalAccessor,
        cancellationToken,
        logger,
        env);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return await req.CreateJsonResponse(HttpStatusCode.TooManyRequests, QuotaExceededBody(ex));
    }
  }

  [Function("AI_QueryAiServiceStream")]
  public async Task<HttpResponseData> QueryAiServiceStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/query-ai-service/stream")] HttpRequestData req,
    CancellationToken cancellationToken)
  {
    try
    {
      return await FunctionExecutionHelper.ExecuteWithAuthAsync(
        req,
        async (_, ct) =>
        {
          var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
          var res = CreateSseResponse(req);
          await WriteSseStreamAsync(res, aiClient.StreamGenerateAsync(query.Prompt, ct), ct);
          return res;
        },
        tokenValidator,
        principalAccessor,
        cancellationToken,
        logger,
        env);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return await req.CreateJsonResponse(HttpStatusCode.TooManyRequests, QuotaExceededBody(ex));
    }
  }

  [Function("AI_PrayerStream")]
  public async Task<HttpResponseData> PrayerStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/prayer/stream")] HttpRequestData req,
    CancellationToken cancellationToken)
  {
    try
    {
      return await FunctionExecutionHelper.ExecuteWithAuthAsync(
        req,
        async (_, ct) =>
        {
          var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
          var res = CreateSseResponse(req);
          await WriteSseStreamAsync(res, aiClient.StreamGeneratePrayerAsync(query.Prompt, ct), ct);
          return res;
        },
        tokenValidator,
        principalAccessor,
        cancellationToken,
        logger,
        env);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return await req.CreateJsonResponse(HttpStatusCode.TooManyRequests, QuotaExceededBody(ex));
    }
  }

  [Function("AI_ChatStream")]
  public async Task<HttpResponseData> ChatStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/chat/stream")] HttpRequestData req,
    CancellationToken cancellationToken)
  {
    try
    {
      return await FunctionExecutionHelper.ExecuteWithAuthAsync(
        req,
        async (_, ct) =>
        {
          var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
          var res = CreateSseResponse(req);
          await WriteSseStreamAsync(res, aiClient.StreamGenerateChatAsync(query.Prompt, ct), ct);
          return res;
        },
        tokenValidator,
        principalAccessor,
        cancellationToken,
        logger,
        env);
    }
    catch (AiMonthlyQuotaExceededException ex)
    {
      return await req.CreateJsonResponse(HttpStatusCode.TooManyRequests, QuotaExceededBody(ex));
    }
  }

  private static HttpResponseData CreateSseResponse(HttpRequestData req)
  {
    var res = req.CreateResponse(HttpStatusCode.OK);
    res.Headers.Add("Content-Type", "text/event-stream; charset=utf-8");
    res.Headers.Add("Cache-Control", "no-cache");
    res.Headers.Add("Connection", "keep-alive");
    res.Headers.Add("X-Accel-Buffering", "no");
    return res;
  }

  private static async Task WriteSseStreamAsync(HttpResponseData response, IAsyncEnumerable<AiStreamPart> parts, CancellationToken cancellationToken)
  {
    await foreach (var part in parts.WithCancellation(cancellationToken))
    {
      var payloadJson = part switch
      {
        AiStreamUsagePart u => JsonSerializer.Serialize(new { aiUsage = u.AiUsage }, StreamJsonOptions),
        AiStreamDeltaPart d => JsonSerializer.Serialize(new { delta = d.Delta }, StreamJsonOptions),
        AiStreamDonePart => JsonSerializer.Serialize(new { done = true }, StreamJsonOptions),
        _ => throw new InvalidOperationException($"Unknown stream part: {part.GetType().Name}")
      };

      await response.WriteStringAsync($"data: {payloadJson}\n\n", cancellationToken);
      await response.Body.FlushAsync(cancellationToken);
    }
  }

  // Note: for streaming endpoints, quota errors can occur before streaming starts.
  // We return a normal JSON 429 in that case.

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

