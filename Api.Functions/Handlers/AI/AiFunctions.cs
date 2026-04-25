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
          // True streaming: extract and emit typed deltas while the model is still generating.
          await WriteTypedEventsFromQueryJsonStreamAsync(
            res,
            aiClient.StreamGenerateAsync(query.Prompt, ct),
            ct);
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

  private static async Task WriteTypedEventsFromQueryJsonStreamAsync(
    HttpResponseData response,
    IAsyncEnumerable<AiStreamPart> parts,
    CancellationToken cancellationToken)
  {
    var extractor = new QueryJsonTypedEventExtractor();
    var sawAnyTypedDelta = false;
    var sawAnyRawDelta = false;
    var raw = new StringBuilder(capacity: 8 * 1024);
    var doneSeen = false;

    await foreach (var part in parts.WithCancellation(cancellationToken))
    {
      switch (part)
      {
        case AiStreamUsagePart u:
          await WriteSseAsync(response, new { aiUsage = u.AiUsage }, cancellationToken);
          break;
        case AiStreamDeltaPart d:
        {
          if (string.IsNullOrEmpty(d.Delta))
            break;

          sawAnyRawDelta = true;
          raw.Append(d.Delta);

          foreach (var ev in extractor.Push(d.Delta))
          {
            sawAnyTypedDelta = true;
            await WriteSseAsync(response, ev, cancellationToken);
          }
          break;
        }
        case AiStreamDonePart:
          doneSeen = true;
          break;
      }
    }

    // If we couldn't safely extract per-field while streaming, fall back to parsing the full JSON
    // and emitting typed deltas in one burst (still typed, no second model call).
    if (!sawAnyTypedDelta && sawAnyRawDelta)
    {
      var text = raw.ToString().Trim();
      try
      {
        // Some models wrap JSON in code fences; strip the common cases.
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
          var firstNl = text.IndexOf('\n');
          if (firstNl >= 0)
          {
            text = text[(firstNl + 1)..];
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
              text = text[..lastFence];
          }
          text = text.Trim();
        }

        var je = JsonSerializer.Deserialize<JsonElement>(text);
        await StreamQueryAiServiceTypedEventsAsync(response, je, cancellationToken);
      }
      catch
      {
        // If parsing fails, we intentionally emit nothing rather than streaming raw JSON blobs.
      }
    }

    if (doneSeen)
      await WriteSseAsync(response, new { type = "done" }, cancellationToken);
  }

  private sealed class QueryJsonTypedEventExtractor
  {
    private readonly List<object> _out = new(capacity: 8);

    // We extract fields in order; this assumes the model outputs JSON in the requested structure.
    private int _fieldIndex;
    private readonly StringPropertyExtractor _text = new("text", "text");
    private readonly StringPropertyExtractor _theologicalMeaning = new("theologicalMeaning", "theologicalMeaning");
    private readonly StringPropertyExtractor _historicalContext = new("historicalContext", "historicalContext");
    private readonly StringPropertyExtractor _devotionalInsight = new("devotionalInsight", "devotionalInsight");
    private readonly StringArrayPropertyExtractor _originalLanguageInsights = new("originalLanguageInsights", "originalLanguageInsights");
    private readonly StringArrayPropertyExtractor _practicalApplications = new("practicalApplications", "practicalApplications");
    private string _buffer = string.Empty;
    private const int MaxBufferChars = 32_768;

    public IEnumerable<object> Push(string chunk)
    {
      _out.Clear();

      if (chunk.Length == 0)
        return _out;

      _buffer = _buffer.Length == 0 ? chunk : _buffer + chunk;
      if (_buffer.Length > MaxBufferChars)
        _buffer = _buffer[^MaxBufferChars..];

      // Drain the current extractor; once it completes, move to the next field.
      var remaining = _buffer;
      while (remaining.Length > 0)
      {
        var beforeLen = remaining.Length;

        switch (_fieldIndex)
        {
          case 0:
            remaining = _text.Push(remaining, _out);
            if (_text.IsComplete) _fieldIndex++;
            break;
          case 1:
            remaining = _theologicalMeaning.Push(remaining, _out);
            if (_theologicalMeaning.IsComplete) _fieldIndex++;
            break;
          case 2:
            remaining = _historicalContext.Push(remaining, _out);
            if (_historicalContext.IsComplete) _fieldIndex++;
            break;
          case 3:
            remaining = _devotionalInsight.Push(remaining, _out);
            if (_devotionalInsight.IsComplete) _fieldIndex++;
            break;
          case 4:
            remaining = _originalLanguageInsights.Push(remaining, _out);
            if (_originalLanguageInsights.IsComplete) _fieldIndex++;
            break;
          case 5:
            remaining = _practicalApplications.Push(remaining, _out);
            if (_practicalApplications.IsComplete) _fieldIndex++;
            break;
          default:
            // We don't emit relatedVerses in typed events yet.
            remaining = string.Empty;
            break;
        }

        // Safety to avoid infinite loops if an extractor doesn't consume.
        if (remaining.Length == beforeLen)
          break;
      }

      _buffer = remaining;
      return _out;
    }
  }

  private abstract class JsonLikeExtractorBase
  {
    protected enum Mode
    {
      SearchingKey,
      SearchingValueStart,
      ReadingString,
      Done
    }

    protected Mode State { get; set; } = Mode.SearchingKey;
    protected int MatchIndex { get; set; }
    protected bool IsEscaped { get; set; }

    protected static int IndexOfOrdinal(string haystack, string needle, int startIndex) =>
      haystack.IndexOf(needle, startIndex, StringComparison.Ordinal);
  }

  private sealed class StringPropertyExtractor : JsonLikeExtractorBase
  {
    private readonly string _jsonKeyNeedle;
    private readonly string _type;

    public bool IsComplete => State == Mode.Done;

    public StringPropertyExtractor(string jsonPropertyName, string type)
    {
      _jsonKeyNeedle = $"\"{jsonPropertyName}\"";
      _type = type;
    }

    public string Push(string input, List<object> output)
    {
      if (State == Mode.Done || input.Length == 0)
        return string.Empty;

      var i = 0;
      while (i < input.Length)
      {
        if (State == Mode.SearchingKey)
        {
          var idx = IndexOfOrdinal(input, _jsonKeyNeedle, i);
          if (idx < 0)
          {
            // Keep a small suffix so key matches can span chunks.
            var keep = Math.Min(input.Length, Math.Max(0, _jsonKeyNeedle.Length - 1));
            return keep == 0 ? string.Empty : input[^keep..];
          }
          i = idx + _jsonKeyNeedle.Length;
          State = Mode.SearchingValueStart;
          continue;
        }

        if (State == Mode.SearchingValueStart)
        {
          // find ':'
          var colon = input.IndexOf(':', i);
          if (colon < 0)
            return input[i..];
          i = colon + 1;

          // skip whitespace
          while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
          if (i >= input.Length) return string.Empty;

          // we only handle string values: "..."
          if (input[i] != '"')
          {
            // If it isn't a string, give up on this field.
            State = Mode.Done;
            return string.Empty;
          }

          i++; // skip opening quote
          State = Mode.ReadingString;
          IsEscaped = false;
          continue;
        }

        if (State == Mode.ReadingString)
        {
          var start = i;
          for (; i < input.Length; i++)
          {
            var ch = input[i];
            if (IsEscaped)
            {
              IsEscaped = false;
              continue;
            }
            if (ch == '\\')
            {
              IsEscaped = true;
              continue;
            }
            if (ch == '"')
            {
              // end of string
              var slice = input.Substring(start, i - start);
              if (slice.Length > 0)
                output.Add(new { type = _type, delta = slice });
              State = Mode.Done;
              return input[(i + 1)..];
            }
          }

          // ran out of input mid-string: emit what we have
          var delta = input.Substring(start);
          if (delta.Length > 0)
            output.Add(new { type = _type, delta });
          return string.Empty;
        }
      }

      return string.Empty;
    }
  }

  private sealed class StringArrayPropertyExtractor : JsonLikeExtractorBase
  {
    private readonly string _jsonKeyNeedle;
    private readonly string _type;
    private int _currentIndex;
    private bool _inArray;

    public bool IsComplete => State == Mode.Done;

    public StringArrayPropertyExtractor(string jsonPropertyName, string type)
    {
      _jsonKeyNeedle = $"\"{jsonPropertyName}\"";
      _type = type;
    }

    public string Push(string input, List<object> output)
    {
      if (State == Mode.Done || input.Length == 0)
        return string.Empty;

      var i = 0;
      while (i < input.Length)
      {
        if (State == Mode.SearchingKey)
        {
          var idx = IndexOfOrdinal(input, _jsonKeyNeedle, i);
          if (idx < 0)
          {
            var keep = Math.Min(input.Length, Math.Max(0, _jsonKeyNeedle.Length - 1));
            return keep == 0 ? string.Empty : input[^keep..];
          }
          i = idx + _jsonKeyNeedle.Length;
          State = Mode.SearchingValueStart;
          continue;
        }

        if (State == Mode.SearchingValueStart)
        {
          var colon = input.IndexOf(':', i);
          if (colon < 0) return input[i..];
          i = colon + 1;
          while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
          if (i >= input.Length) return string.Empty;
          if (input[i] != '[')
          {
            State = Mode.Done;
            return string.Empty;
          }
          i++; // skip '['
          _inArray = true;
          State = Mode.ReadingString; // reuse as item scanning mode
          continue;
        }

        if (State == Mode.ReadingString)
        {
          // scan until we find either a string item, ']' end, or consume partial string
          while (i < input.Length && _inArray)
          {
            while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
            if (i >= input.Length) return string.Empty;

            if (input[i] == ']')
            {
              i++;
              _inArray = false;
              State = Mode.Done;
              return input[i..];
            }

            if (input[i] == ',')
            {
              i++;
              continue;
            }

            if (input[i] != '"')
            {
              // skip unexpected tokens
              i++;
              continue;
            }

            // string item
            i++; // opening quote
            var start = i;
            IsEscaped = false;
            for (; i < input.Length; i++)
            {
              var ch = input[i];
              if (IsEscaped)
              {
                IsEscaped = false;
                continue;
              }
              if (ch == '\\')
              {
                IsEscaped = true;
                continue;
              }
              if (ch == '"')
              {
                var slice = input.Substring(start, i - start);
                output.Add(new { type = _type, index = _currentIndex, delta = slice });
                _currentIndex++;
                i++; // closing quote
                break;
              }
            }

            if (i >= input.Length)
            {
              // partial string item
              var delta = input.Substring(start);
              if (delta.Length > 0)
                output.Add(new { type = _type, index = _currentIndex, delta });
              return string.Empty;
            }
          }
        }
      }

      return string.Empty;
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

    // Our prompt formats often wrap the payload under a "response" object.
    var payloadObj = je;
    if (je.TryGetProperty("response", out var responseObj) && responseObj.ValueKind == JsonValueKind.Object)
      payloadObj = responseObj;

    // Expected keys based on your existing prompt formats.
    await StreamJsonStringPropertyAsync(res, payloadObj, jsonPropertyName: "text", type: "text", index: null, ct);
    await StreamJsonStringPropertyAsync(res, payloadObj, jsonPropertyName: "theologicalMeaning", type: "theologicalMeaning", index: null, ct);
    await StreamJsonStringPropertyAsync(res, payloadObj, jsonPropertyName: "historicalContext", type: "historicalContext", index: null, ct);
    await StreamJsonStringPropertyAsync(res, payloadObj, jsonPropertyName: "devotionalInsight", type: "devotionalInsight", index: null, ct);

    // Arrays (typed events require index).
    await StreamJsonStringArrayPropertyAsync(res, payloadObj, jsonPropertyName: "originalLanguageInsights", type: "originalLanguageInsights", ct);
    await StreamJsonStringArrayPropertyAsync(res, payloadObj, jsonPropertyName: "practicalApplications", type: "practicalApplications", ct);
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
