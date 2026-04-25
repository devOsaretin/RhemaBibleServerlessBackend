using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class AiFunctions(
  IAIClient aiClient,
  IUserApplicationService userService,
  IFunctionTokenValidator tokenValidator,
  ICurrentPrincipalAccessor principalAccessor,
  ILogger<AiFunctions> logger,
  IHostEnvironment env)
{
  private static readonly JsonSerializerOptions StreamJsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  private const int DefaultDeltaChunkSize = 96;

  [Function("AI_QueryAiService")]
  public Task<HttpResponseData> QueryAiService(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/query-ai-service")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
        var result = await aiClient.GenerateAsync(query.Prompt, ct);
        return await req.CreateJsonResponse(HttpStatusCode.OK, new { data = result.Data, aiUsage = result.AiUsage });
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_Prayer")]
  public Task<HttpResponseData> Prayer(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/prayer")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
        var result = await aiClient.GeneratePrayerAsync(query.Prompt, ct);
        return await req.CreateJsonResponse(HttpStatusCode.OK, new { data = result.Data, aiUsage = result.AiUsage });
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_Chat")]
  public Task<HttpResponseData> Chat(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/chat")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
        var result = await aiClient.GenerateChatAsync(query.Prompt, ct);
        return await req.CreateJsonResponse(HttpStatusCode.OK, new { data = result.Data, aiUsage = result.AiUsage });
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_QueryAiServiceStream")]
  public Task<HttpResponseData> QueryAiServiceStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/query-ai-service/stream")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
        var res = CreateSseResponse(req);
        try
        {
          // The frontend consumes typed SSE events; this endpoint emits per-field typed deltas.
          // The current AI client streams plain text only, so we generate the structured payload first,
          // then stream small typed deltas per field.
          var result = await aiClient.GenerateAsync(query.Prompt, ct);
          await WriteSseAsync(res, new { aiUsage = result.AiUsage }, ct);
          await StreamQueryAiServiceTypedEventsAsync(res, result.Data, ct);
          await WriteSseAsync(res, new { type = "done" }, ct);
        }
        catch (Exception ex)
        {
          await WriteSseAsync(res, new { error = ex.Message }, ct);
        }
        return res;
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_PrayerStream")]
  public Task<HttpResponseData> PrayerStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/prayer/stream")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
        var res = CreateSseResponse(req);
        await WriteSseStreamAsync(res, aiClient.StreamGeneratePrayerAsync(query.Prompt, ct), defaultDeltaType: "prayer", ct);
        return res;
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_ChatStream")]
  public Task<HttpResponseData> ChatStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/chat/stream")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var query = await req.ReadRequiredJsonAsync<ChatRequest>(ct);
        var res = CreateSseResponse(req);
        await WriteSseStreamAsync(res, aiClient.StreamGenerateChatAsync(query.Prompt, ct), defaultDeltaType: "text", ct);
        return res;
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_ConversationGospelChatStream")]
  public Task<HttpResponseData> ConversationGospelChatStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/conversation-gospel/stream")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var body = await req.ReadRequiredJsonAsync<ConversationGospelChatStreamRequest>(ct);
        var messages = ConversationGospelChatValidator.NormalizeOrThrow(body.Messages);
        var res = CreateSseResponse(req);
        await WriteSseStreamAsync(res, aiClient.StreamGenerateConversationGospelChatAsync(messages, ct), defaultDeltaType: "text", ct);
        return res;
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_ApplyVerse")]
  public Task<HttpResponseData> ApplyVerse(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/apply-verse")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var body = await req.ReadRequiredJsonAsync<ApplyVerseRequest>(ct);
        var result = await aiClient.GenerateApplyVerseAsync(body, ct);
        return await req.CreateJsonResponse(HttpStatusCode.OK, new { data = result.Data, aiUsage = result.AiUsage });
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  [Function("AI_ApplyVerseStream")]
  public Task<HttpResponseData> ApplyVerseStream(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ai/apply-verse/stream")] HttpRequestData req,
    CancellationToken cancellationToken) =>
    FunctionExecutionHelper.ExecuteWithAuthAsync(
      req,
      async (_, ct) =>
      {
        var body = await req.ReadRequiredJsonAsync<ApplyVerseRequest>(ct);
        var res = CreateSseResponse(req);
        try
        {
          var result = await aiClient.GenerateApplyVerseAsync(body, ct);
          await WriteSseAsync(res, new { aiUsage = result.AiUsage }, ct);
          await StreamApplyVerseTypedEventsAsync(res, result.Data, ct);
          await WriteSseAsync(res, new { type = "done" }, ct);
        }
        catch (Exception ex)
        {
          await WriteSseAsync(res, new { error = ex.Message }, ct);
        }

        return res;
      },
      tokenValidator,
      principalAccessor,
      userService,
      cancellationToken,
      logger,
      env);

  private static HttpResponseData CreateSseResponse(HttpRequestData req)
  {
    var res = req.CreateResponse(HttpStatusCode.OK);
    res.Headers.Add("Content-Type", "text/event-stream; charset=utf-8");
    res.Headers.Add("Cache-Control", "no-cache");
    res.Headers.Add("Connection", "keep-alive");
    res.Headers.Add("X-Accel-Buffering", "no");
    return res;
  }

  private static async Task WriteSseAsync(HttpResponseData response, object payload, CancellationToken cancellationToken)
  {
    var json = JsonSerializer.Serialize(payload, StreamJsonOptions);
    var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
    await response.Body.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
  }

  private static async Task WriteSseStreamAsync(
    HttpResponseData response,
    IAsyncEnumerable<AiStreamPart> parts,
    string defaultDeltaType,
    CancellationToken cancellationToken)
  {
    await foreach (var part in parts.WithCancellation(cancellationToken))
    {
      switch (part)
      {
        case AiStreamUsagePart u:
          await WriteSseAsync(response, new { aiUsage = u.AiUsage }, cancellationToken);
          break;
        case AiStreamDeltaPart d:
          if (!string.IsNullOrEmpty(d.Delta))
            await WriteSseAsync(response, new { type = defaultDeltaType, delta = d.Delta }, cancellationToken);
          break;
        case AiStreamDonePart:
          await WriteSseAsync(response, new { type = "done" }, cancellationToken);
          break;
        default:
          throw new InvalidOperationException($"Unknown stream part: {part.GetType().Name}");
      }
    }
  }

  private static async Task StreamQueryAiServiceTypedEventsAsync(HttpResponseData res, object data, CancellationToken ct)
  {
    if (data is not JsonElement je || je.ValueKind != JsonValueKind.Object)
    {
      // Best-effort fallback: treat as plain text.
      await StreamStringAsTypedDeltasAsync(res, type: "text", value: data?.ToString() ?? string.Empty, index: null, ct);
      return;
    }

    // Expected keys based on your existing prompt formats.
    await StreamJsonStringPropertyAsync(res, je, jsonPropertyName: "text", type: "text", index: null, ct);
    await StreamJsonStringPropertyAsync(res, je, jsonPropertyName: "theologicalMeaning", type: "theologicalMeaning", index: null, ct);
    await StreamJsonStringPropertyAsync(res, je, jsonPropertyName: "historicalContext", type: "historicalContext", index: null, ct);
    await StreamJsonStringPropertyAsync(res, je, jsonPropertyName: "devotionalInsight", type: "devotionalInsight", index: null, ct);

    // Arrays (typed events require index).
    await StreamJsonStringArrayPropertyAsync(res, je, jsonPropertyName: "originalLanguageInsights", type: "originalLanguageInsights", ct);
    await StreamJsonStringArrayPropertyAsync(res, je, jsonPropertyName: "practicalApplications", type: "practicalApplications", ct);
  }

  private static async Task StreamApplyVerseTypedEventsAsync(HttpResponseData res, object data, CancellationToken ct)
  {
    if (data is not JsonElement je || je.ValueKind != JsonValueKind.Object)
    {
      await StreamStringAsTypedDeltasAsync(res, type: "lifeInsight", value: data?.ToString() ?? string.Empty, index: null, ct);
      return;
    }

    await StreamJsonStringPropertyAsync(res, je, jsonPropertyName: "lifeInsight", type: "lifeInsight", index: null, ct);
    await StreamJsonStringPropertyAsync(res, je, jsonPropertyName: "practicalAction", type: "practicalAction", index: null, ct);
    await StreamJsonStringPropertyAsync(res, je, jsonPropertyName: "prayer", type: "prayer", index: null, ct);
    await StreamJsonStringArrayPropertyAsync(res, je, jsonPropertyName: "supportingVerses", type: "supportingVerses", ct);
  }

  private static async Task StreamJsonStringPropertyAsync(
    HttpResponseData res,
    JsonElement obj,
    string jsonPropertyName,
    string type,
    int? index,
    CancellationToken ct)
  {
    if (!obj.TryGetProperty(jsonPropertyName, out var prop))
      return;

    var s = prop.ValueKind switch
    {
      JsonValueKind.String => prop.GetString() ?? string.Empty,
      JsonValueKind.Null => string.Empty,
      _ => prop.GetRawText()
    };

    await StreamStringAsTypedDeltasAsync(res, type, s, index, ct);
  }

  private static async Task StreamJsonStringArrayPropertyAsync(
    HttpResponseData res,
    JsonElement obj,
    string jsonPropertyName,
    string type,
    CancellationToken ct)
  {
    if (!obj.TryGetProperty(jsonPropertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
      return;

    var idx = 0;
    foreach (var item in prop.EnumerateArray())
    {
      var s = item.ValueKind switch
      {
        JsonValueKind.String => item.GetString() ?? string.Empty,
        JsonValueKind.Null => string.Empty,
        _ => item.GetRawText()
      };

      await StreamStringAsTypedDeltasAsync(res, type, s, index: idx, ct);
      idx++;
    }
  }

  private static async Task StreamStringAsTypedDeltasAsync(
    HttpResponseData res,
    string type,
    string value,
    int? index,
    CancellationToken ct)
  {
    if (string.IsNullOrEmpty(value))
      return;

    foreach (var chunk in ChunkString(value, DefaultDeltaChunkSize))
    {
      if (index is null)
        await WriteSseAsync(res, new { type, delta = chunk }, ct);
      else
        await WriteSseAsync(res, new { type, index, delta = chunk }, ct);
    }
  }

  private static IEnumerable<string> ChunkString(string s, int chunkSize)
  {
    if (chunkSize <= 0)
      chunkSize = DefaultDeltaChunkSize;

    // Keep whitespace; the frontend concatenates deltas.
    for (var i = 0; i < s.Length; i += chunkSize)
    {
      var len = Math.Min(chunkSize, s.Length - i);
      yield return s.Substring(i, len);
    }
  }
}
