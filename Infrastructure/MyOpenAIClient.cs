using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Domain.Enums;
using RhemaBibleAppServerless.Domain.Models;

public class OpenAIChatMessage
{
    public required string role { get; set; }
    public required string content { get; set; }
}

public class OpenAIChatRequest
{
    public required string model { get; set; }
    public List<OpenAIChatMessage> messages { get; set; } = new();
    public bool stream { get; set; } = false;
    public float temperature { get; set; } = 0.7f;
}

public class MyOpenAIClient : IAIClient
{
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string QueryCacheKeyPrefix = "ai:query:v1:";
    private const float DefaultTemperature = 0.7f;

    private static readonly JsonSerializerOptions PromptKeyJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserPersistence _userPersistence;
    private readonly IServiceBusService _serviceBusService;
    private readonly IAiQuotaService _aiQuotaService;
    private readonly IPromptFileReader _promptFiles;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptionsMonitor<AiQueryCacheOptions> _queryCacheOptions;
    private readonly string _model;

    public MyOpenAIClient(
        ICurrentUserService currentUserService,
        IUserPersistence userPersistence,
        IServiceBusService serviceBusService,
        IAiQuotaService aiQuotaService,
        IPromptFileReader promptFiles,
        IMemoryCache memoryCache,
        IOptionsMonitor<AiQueryCacheOptions> queryCacheOptions,
        HttpClient httpClient,
        string model = "gpt-4o-mini")
    {
        _currentUserService = currentUserService;
        _userPersistence = userPersistence;
        _httpClient = httpClient;
        _model = model;
        _serviceBusService = serviceBusService;
        _aiQuotaService = aiQuotaService;
        _promptFiles = promptFiles;
        _memoryCache = memoryCache;
        _queryCacheOptions = queryCacheOptions;
    }

    /// <summary>
    /// Subscription and quota decisions must use a database-backed user row. The app-layer user cache can lag
    /// for a short window after purchase, which previously caused premium accounts to consume free AI quota.
    /// </summary>
    private async Task<(User User, bool HasActivePremium)> GetUserForAiAsync(CancellationToken cancellationToken)
    {
        var principalUser = await _currentUserService.GetUserAsync(cancellationToken);
        var fresh = await _userPersistence.GetByIdAsync(principalUser.Id!, cancellationToken);
        var user = fresh ?? principalUser;
        return (user, user.HasActivePremiumSubscription());
    }

    public async Task<AiClientResult> GenerateAsync(string query, CancellationToken cancellationToken = default)
    {
        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);

        // Subscribers: always premium prompts. Free tier within monthly allowance: same premium prompts.
        var prompt = PromptHelper.GeneratePrompt(_promptFiles, query, user.FirstName);

        var cacheOpts = _queryCacheOptions.CurrentValue;
        if (cacheOpts.Enabled &&
            TryGetCachedQueryResult(prompt, out var cachedDataJson))
        {
            var usageNoConsume = hasActivePremium
                ? new AiUsageDto { IsUnlimited = true }
                : _aiQuotaService.BuildUsageSnapshot(user);
            return new AiClientResult(ParseCachedDataJson(cachedDataJson!), usageNoConsume);
        }

        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);

        var payload = BuildPayload(prompt, stream: false);

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            throw new InvalidOperationException("HttpClient is missing the Authorization header.");
        }

        var response = await _httpClient.PostAsJsonAsync(ApiUrl, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI API call failed: {response.StatusCode}, {body}");
        }

        // Intentionally do not log AI chats/analysis as recent activity.

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var data = ParseChoicesContent(json, fallbackPropertyName: "text");
        SetCachedQueryResult(prompt, data, cacheOpts);
        return new AiClientResult(data, usage);
    }

    public async Task<AiClientResult> GeneratePrayerAsync(string query, CancellationToken cancellationToken = default)
    {
        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);

        var prompt = PromptHelper.GeneratePrayerPrompt(_promptFiles, query, user.FirstName);

        var payload = BuildPayload(prompt, stream: false);

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            throw new InvalidOperationException("HttpClient is missing the Authorization header.");
        }

        var response = await _httpClient.PostAsJsonAsync(ApiUrl, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI API call failed: {response.StatusCode}, {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var data = ParseChoicesContent(json, fallbackPropertyName: "prayer");
        return new AiClientResult(data, usage);
    }

    public async Task<AiClientResult> GenerateChatAsync(string query, CancellationToken cancellationToken = default)
    {
        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);

        var prompt = PromptHelper.GenerateChatPrompt(_promptFiles, query, user.FirstName);

        var payload = BuildPayload(prompt, stream: false);

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            throw new InvalidOperationException("HttpClient is missing the Authorization header.");
        }

        var response = await _httpClient.PostAsJsonAsync(ApiUrl, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI API call failed: {response.StatusCode}, {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var data = ParseChoicesContent(json, fallbackPropertyName: "prayer");
        return new AiClientResult(data, usage);
    }

    public async Task<AiClientResult> GenerateApplyVerseAsync(
        ApplyVerseRequest request,
        CancellationToken cancellationToken = default)
    {
        var (reference, verseText, userNote) = ApplyVerseRequestValidator.NormalizeOrThrow(request);

        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);

        var prompt = PromptHelper.GenerateApplyVersePrompt(_promptFiles, reference, verseText, userNote, user.FirstName);

        var payload = BuildPayload(prompt, stream: false);

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            throw new InvalidOperationException("HttpClient is missing the Authorization header.");
        }

        var response = await _httpClient.PostAsJsonAsync(ApiUrl, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI API call failed: {response.StatusCode}, {body}");
        }

        await LogReflectVerseActivity(user.Id!, reference, cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var data = ParseChoicesContent(json, fallbackPropertyName: "lifeInsight");
        return new AiClientResult(data, usage);
    }

    public async IAsyncEnumerable<AiStreamPart> StreamGenerateAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);
        yield return new AiStreamUsagePart(usage);

        var prompt = PromptHelper.GeneratePrompt(_promptFiles, query, user.FirstName);
        await foreach (var part in StreamCompletionCoreAsync(user.Id!, query, prompt, cancellationToken))
            yield return part;
    }

    public async IAsyncEnumerable<AiStreamPart> StreamGeneratePrayerAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);
        yield return new AiStreamUsagePart(usage);

        var prompt = PromptHelper.GeneratePrayerPrompt(_promptFiles, query, user.FirstName);
        await foreach (var part in StreamCompletionCoreAsync(user.Id!, query, prompt, cancellationToken))
            yield return part;
    }

    public async IAsyncEnumerable<AiStreamPart> StreamGenerateChatAsync(
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);
        yield return new AiStreamUsagePart(usage);

        var prompt = PromptHelper.GenerateChatPrompt(_promptFiles, query, user.FirstName);
        await foreach (var part in StreamCompletionCoreAsync(user.Id!, query, prompt, cancellationToken))
            yield return part;
    }

    public async IAsyncEnumerable<AiStreamPart> StreamGenerateApplyVerseAsync(
        ApplyVerseRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (reference, verseText, userNote) = ApplyVerseRequestValidator.NormalizeOrThrow(request);

        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);
        yield return new AiStreamUsagePart(usage);

        // Log early so streaming UX isn't blocked on the end of generation.
        await LogReflectVerseActivity(user.Id!, reference, cancellationToken);

        var prompt = PromptHelper.GenerateApplyVersePrompt(_promptFiles, reference, verseText, userNote, user.FirstName);
        var userQueryForLog = $"Apply verse: {reference}";

        await foreach (var part in StreamCompletionCoreAsync(user.Id!, userQueryForLog, prompt, cancellationToken))
            yield return part;
    }

    public async IAsyncEnumerable<AiStreamPart> StreamGenerateConversationGospelChatAsync(
        IReadOnlyList<ChatMessageDto> conversationMessages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (user, hasActivePremium) = await GetUserForAiAsync(cancellationToken);
        var usage = await ResolveUsageAsync(user, hasActivePremium, cancellationToken);
        yield return new AiStreamUsagePart(usage);

        var prompt = PromptHelper.GenerateConversationGospelChatPrompt(_promptFiles, conversationMessages, user.FirstName);

        var userQueryForLog = conversationMessages
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content ?? "(conversation)";

        await foreach (var part in StreamCompletionCoreAsync(user.Id!, userQueryForLog, prompt, cancellationToken))
            yield return part;
    }

    private async IAsyncEnumerable<AiStreamPart> StreamCompletionCoreAsync(
        string userId,
        string userQuery,
        List<ChatMessageDto> promptMessages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var payload = BuildPayload(promptMessages, stream: true);

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            throw new InvalidOperationException("HttpClient is missing the Authorization header.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = JsonContent.Create(payload)
        };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI API call failed: {response.StatusCode}, {body}");
        }

        // Intentionally do not log AI chats/analysis as recent activity.

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (
            var delta in ReadChatCompletionSseStreamAsync(responseStream, cancellationToken)
                .WithCancellation(cancellationToken))
        {
            yield return new AiStreamDeltaPart(delta);
        }

        yield return new AiStreamDonePart();
    }

    private static async IAsyncEnumerable<string> ReadChatCompletionSseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;
            if (line.Length == 0)
                continue;
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]")
                yield break;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(data);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    continue;

                var choice = choices[0];
                if (!choice.TryGetProperty("delta", out var delta))
                    continue;
                if (!delta.TryGetProperty("content", out var contentEl))
                    continue;

                var piece = contentEl.GetString();
                if (!string.IsNullOrEmpty(piece))
                    yield return piece;
            }
        }
    }

    private async Task<AiUsageDto> ResolveUsageAsync(User user, bool hasActivePremium, CancellationToken cancellationToken)
    {
        if (hasActivePremium)
        {
            return new AiUsageDto { IsUnlimited = true };
        }

        var consumed = await _aiQuotaService.ConsumeFreeCallAsync(user.Id!, cancellationToken);
        return new AiUsageDto
        {
            IsUnlimited = false,
            FreeCallsRemainingThisMonth = consumed.FreeCallsRemainingThisMonth,
            FreeCallsLimitPerMonth = _aiQuotaService.FreeCallsPerMonth,
            FreeCallsUsedThisMonth = consumed.FreeCallsUsedThisMonth,
            MonthKeyUtc = consumed.MonthKeyUtc
        };
    }

    private OpenAIChatRequest BuildPayload(List<ChatMessageDto> promptMessages, bool stream)
    {
        var openAiMessages = promptMessages.Select(p => new OpenAIChatMessage
        {
            role = p.Role.ToLowerInvariant(),
            content = p.Content
        }).ToList();

        return new OpenAIChatRequest
        {
            model = _model,
            messages = openAiMessages,
            stream = stream,
            temperature = DefaultTemperature
        };
    }

    private string BuildQueryCacheKey(List<ChatMessageDto> promptMessages)
    {
        var promptJson = JsonSerializer.Serialize(promptMessages, PromptKeyJsonOptions);
        var payload = $"{_model}\n{DefaultTemperature}\n{promptJson}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return QueryCacheKeyPrefix + hash;
    }

    private bool TryGetCachedQueryResult(List<ChatMessageDto> promptMessages, out string? dataJson)
    {
        var key = BuildQueryCacheKey(promptMessages);
        return _memoryCache.TryGetValue(key, out dataJson);
    }

    private void SetCachedQueryResult(List<ChatMessageDto> promptMessages, object data, AiQueryCacheOptions cacheOpts)
    {
        if (!cacheOpts.Enabled)
            return;

        var hours = cacheOpts.ExpirationHours <= 0 ? 24 : cacheOpts.ExpirationHours;
        var dataJson = SerializeDataForCache(data);
        var key = BuildQueryCacheKey(promptMessages);
        _memoryCache.Set(
            key,
            dataJson,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(hours)
            });
    }

    private static string SerializeDataForCache(object data)
    {
        if (data is JsonElement je)
            return je.GetRawText();
        return JsonSerializer.Serialize(data, PromptKeyJsonOptions);
    }

    private static JsonElement ParseCachedDataJson(string dataJson)
    {
        using var doc = JsonDocument.Parse(dataJson);
        return doc.RootElement.Clone();
    }

    private static object ParseChoicesContent(JsonElement json, string fallbackPropertyName)
    {
        if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                var result = content.GetString()?.Trim() ?? string.Empty;

                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(result)!;
                }
                catch
                {
                    return JsonSerializer.SerializeToElement(new Dictionary<string, string>
                    {
                        [fallbackPropertyName] = result
                    });
                }
            }
        }

        throw new InvalidOperationException("Unexpected OpenAI response structure: " + json);
    }

    private Task LogReflectVerseActivity(string userId, string reference, CancellationToken cancellationToken)
    {
        var activity = new AddActivityToQueueDto
        {
            AuthId = userId,
            ActivityType = ActivityType.AIAnalysis.ToString(),
            Title = $"Reflect Verse: {reference}"
        };
        return _serviceBusService.PublishAsync(activity, QueueNames.Activity, cancellationToken);
    }
}
