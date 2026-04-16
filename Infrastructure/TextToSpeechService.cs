using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Amazon.Polly;
using Amazon.Polly.Model;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Azure.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;



public sealed class TextToSpeechService : ITextToSpeechService
{
    private const string MemoryKeyPrefix = "tts:v1:exists:";
    private const string BlobMetadataEngineKey = "ttsengine";
    private const string VerseAnyVoicePrefix = "verse";

    private static readonly ElevenLabsVoiceSettings AsherVoiceSettings = new()
    {
        Stability = 0.6,
        SimilarityBoost = 0.75,
        Style = 0,
        UseSpeakerBoost = true
    };

    private static readonly ElevenLabsVoiceSettings GrandpaVoiceSettings = new()
    {
        Stability = 0.4,
        SimilarityBoost = 0.8,
        Style = 0.2,
        UseSpeakerBoost = true
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAmazonPolly _polly;
    private readonly IOptionsMonitor<ElevenLabsTtsOptions> _elevenLabsOptions;
    private readonly IOptionsMonitor<PollyTtsOptions> _pollyOptions;
    private readonly IOptionsMonitor<TextToSpeechRoutingOptions> _routingOptions;
    private readonly IOptionsMonitor<AzureBlobTtsOptions> _blobOptions;
    private readonly ILogger<TextToSpeechService> _logger;

    public TextToSpeechService(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        ICurrentUserService currentUserService,
        IAmazonPolly polly,
        IOptionsMonitor<ElevenLabsTtsOptions> elevenLabsOptions,
        IOptionsMonitor<PollyTtsOptions> pollyOptions,
        IOptionsMonitor<TextToSpeechRoutingOptions> routingOptions,
        IOptionsMonitor<AzureBlobTtsOptions> blobOptions,
        ILogger<TextToSpeechService> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _currentUserService = currentUserService;
        _polly = polly;
        _elevenLabsOptions = elevenLabsOptions;
        _pollyOptions = pollyOptions;
        _routingOptions = routingOptions;
        _blobOptions = blobOptions;
        _logger = logger;
    }

    public async Task<TextToSpeechResponse> SynthesizeAsync(TextToSpeechRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _currentUserService.GetUserAsync(cancellationToken);
        if (string.IsNullOrEmpty(user.Id))
            throw new InvalidOperationException("User id is required for text-to-speech.");

        var blobOpts = _blobOptions.CurrentValue;
        if (string.IsNullOrWhiteSpace(blobOpts.ConnectionString))
            throw new InvalidOperationException("AzureBlobTts:ConnectionString is not configured.");

        var usePolly = ShouldUsePolly(user);
        var text = request.Text.Trim();
        if (text.Length == 0)
            throw new ArgumentException("Text is required.", nameof(request));

        string contentHash;
        string sourceApi;
        var verseHash = BuildVerseHash(text);

        if (usePolly)
        {
            var polly = _pollyOptions.CurrentValue;
            var voiceId = string.IsNullOrWhiteSpace(request.VoiceId) ? polly.DefaultVoiceId : request.VoiceId.Trim();
            var engineName = polly.Engine.Trim();
            contentHash = BuildPollyContentHash(voiceId, engineName, text);
            sourceApi = "polly";
        }
        else
        {
            var el = _elevenLabsOptions.CurrentValue;
            if (string.IsNullOrWhiteSpace(el.ApiKey))
                throw new InvalidOperationException("ElevenLabs:ApiKey is not configured.");

            var (voiceId, voiceSettings) = ResolveElevenLabsVoiceSelection(el, request.Book, request.VoiceId);
            var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? el.DefaultModelId : request.ModelId!.Trim();
            if (string.IsNullOrWhiteSpace(voiceId))
                throw new InvalidOperationException("Provide VoiceId or set ElevenLabs:DefaultVoiceId in configuration.");

            var outputFormat = el.OutputFormat.Trim();
            contentHash = BuildElevenLabsContentHash(voiceId, modelId, outputFormat, voiceSettings, text);
            sourceApi = "elevenlabs";
        }

        var anyVoiceMemKey = MemoryKeyPrefix + "anyvoice:" + verseHash;
        var memKey = MemoryKeyPrefix + contentHash;
        var (containerClient, blobClient) = CreateBlobClients(blobOpts, contentHash);
        var (_, anyVoiceBlobClient) = CreateBlobClients(blobOpts, $"{VerseAnyVoicePrefix}/{verseHash}");

        if (_memoryCache.TryGetValue(anyVoiceMemKey, out string? cachedAnyEngine) && !string.IsNullOrWhiteSpace(cachedAnyEngine))
            return BuildCachedResponse(anyVoiceBlobClient, blobOpts, verseHash, cachedAnyEngine, "memory-any-voice");

        if (await anyVoiceBlobClient.ExistsAsync(cancellationToken))
        {
            var props = await anyVoiceBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var engine = TryGetEngineFromBlobMetadata(props.Value.Metadata, sourceApi);

            _memoryCache.Set(anyVoiceMemKey, engine, TimeSpan.FromMinutes(blobOpts.MemoryCacheExpirationMinutes));
            return BuildCachedResponse(anyVoiceBlobClient, blobOpts, verseHash, engine, "blob-any-voice");
        }

        if (_memoryCache.TryGetValue(memKey, out string? cachedEngine) && !string.IsNullOrWhiteSpace(cachedEngine))
            return BuildCachedResponse(blobClient, blobOpts, contentHash, cachedEngine, "memory");

        if (await blobClient.ExistsAsync(cancellationToken))
        {
            var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var engine = TryGetEngineFromBlobMetadata(props.Value.Metadata, sourceApi);

            _memoryCache.Set(memKey, engine, TimeSpan.FromMinutes(blobOpts.MemoryCacheExpirationMinutes));
            return BuildCachedResponse(blobClient, blobOpts, contentHash, engine, "blob");
        }

        byte[] audio;
        if (usePolly)
        {
            var polly = _pollyOptions.CurrentValue;
            var voiceId = string.IsNullOrWhiteSpace(request.VoiceId) ? polly.DefaultVoiceId : request.VoiceId.Trim();
            var engineName = polly.Engine.Trim();
            audio = await CallPollyAsync(voiceId, engineName, text, cancellationToken);
        }
        else
        {
            var el = _elevenLabsOptions.CurrentValue;
            var (voiceId, voiceSettings) = ResolveElevenLabsVoiceSelection(el, request.Book, request.VoiceId);
            var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? el.DefaultModelId : request.ModelId!.Trim();
            var outputFormat = el.OutputFormat.Trim();
            audio = await CallElevenLabsAsync(el, voiceId, modelId, text, outputFormat, voiceSettings, cancellationToken);
        }

        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        await using var ms = new MemoryStream(audio, writable: false);
        await blobClient.UploadAsync(
            ms,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "audio/mpeg" },
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [BlobMetadataEngineKey] = sourceApi
                }
            },
            cancellationToken: cancellationToken);

        await using var msAny = new MemoryStream(audio, writable: false);
        if (!await anyVoiceBlobClient.ExistsAsync(cancellationToken))
        {
            await anyVoiceBlobClient.UploadAsync(
                msAny,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "audio/mpeg" },
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [BlobMetadataEngineKey] = sourceApi
                    },
                    Conditions = new BlobRequestConditions { IfNoneMatch = new ETag("*") }
                },
                cancellationToken: cancellationToken);
        }

        _memoryCache.Set(anyVoiceMemKey, sourceApi, TimeSpan.FromMinutes(blobOpts.MemoryCacheExpirationMinutes));
        _memoryCache.Set(memKey, sourceApi, TimeSpan.FromMinutes(blobOpts.MemoryCacheExpirationMinutes));

        var freshUrl = blobClient.GenerateSasUri(
            BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddMinutes(blobOpts.SasExpiryMinutes));

        _logger.LogInformation("TTS generated via {Api} for hash {Hash}", sourceApi, contentHash);

        return new TextToSpeechResponse
        {
            AudioUrl = freshUrl.AbsoluteUri,
            ContentHash = contentHash,
            ServedFromCache = false,
            Source = sourceApi,
            CacheSource = null
        };
    }

    private bool ShouldUsePolly(User user)
    {
        if (user.HasActivePremiumSubscription())
            return false;
        return _routingOptions.CurrentValue.UsePollyForNonProSubscribers;
    }

    private static string BuildElevenLabsContentHash(
        string voiceId,
        string modelId,
        string outputFormat,
        ElevenLabsVoiceSettings voiceSettings,
        string text)
    {
        var vs = string.Create(
            CultureInfo.InvariantCulture,
            $"{voiceSettings.Stability}:{voiceSettings.SimilarityBoost}:{voiceSettings.Style}:{voiceSettings.UseSpeakerBoost}");
        var payload = $"elevenlabs\n{voiceId}\n{modelId}\n{outputFormat}\n{vs}\n{text}";
        return HashPayload(payload);
    }

    private static string BuildPollyContentHash(string voiceId, string engineName, string text)
    {
        var payload = $"polly\n{voiceId}\n{engineName}\n{text}";
        return HashPayload(payload);
    }

    private static string HashPayload(string payload)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string BuildVerseHash(string text)
    {
        var payload = $"verse\n{text}";
        return HashPayload(payload);
    }

    private static string TryGetEngineFromBlobMetadata(IDictionary<string, string> metadata, string fallback)
    {
        foreach (var kv in metadata)
        {
            if (!string.Equals(kv.Key, BlobMetadataEngineKey, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kv.Key, "tts-engine", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(kv.Value))
                return kv.Value;
        }

        return fallback;
    }

    private static (string VoiceId, ElevenLabsVoiceSettings VoiceSettings) ResolveElevenLabsVoiceSelection(
        ElevenLabsTtsOptions el,
        string? book,
        string? requestVoiceId)
    {
        if (!string.IsNullOrWhiteSpace(requestVoiceId))
        {
            var requested = requestVoiceId.Trim();
            return (requested, el.VoiceSettings ?? new ElevenLabsVoiceSettings());
        }

        var defaultVoiceId = el.DefaultVoiceId?.Trim() ?? string.Empty;

        var grandpaVoiceId = el.GrandpaVoiceId?.Trim() ?? string.Empty;
        var asherVoiceId = el.AsherVoiceId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(grandpaVoiceId) || string.IsNullOrWhiteSpace(asherVoiceId))
            return (defaultVoiceId, el.VoiceSettings ?? new ElevenLabsVoiceSettings());

        var bookKey = CanonicalizeBookKey(book);
        if (bookKey.Length == 0)
            return (defaultVoiceId, el.VoiceSettings ?? new ElevenLabsVoiceSettings());

        var grandpaBooks = new HashSet<string>(StringComparer.Ordinal)
        {
            CanonicalizeBookKey("Psalms"),
            CanonicalizeBookKey("Proverbs"),
            CanonicalizeBookKey("Song of Solomon"),
        };

        if (grandpaBooks.Contains(bookKey))
            return (grandpaVoiceId, GrandpaVoiceSettings);

        return (asherVoiceId, AsherVoiceSettings);
    }

    private static string CanonicalizeBookKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var s = value.Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private TextToSpeechResponse BuildCachedResponse(
        BlobClient blobClient,
        AzureBlobTtsOptions blobOpts,
        string contentHash,
        string engine,
        string cacheSource)
    {
        var url = blobClient.GenerateSasUri(
            BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddMinutes(blobOpts.SasExpiryMinutes));

        _logger.LogInformation("TTS served from {CacheSource} cache for hash {Hash}", cacheSource, contentHash);

        return new TextToSpeechResponse
        {
            AudioUrl = url.AbsoluteUri,
            ContentHash = contentHash,
            ServedFromCache = true,
            Source = engine,
            CacheSource = cacheSource
        };
    }

    private static (BlobContainerClient Container, BlobClient Blob) CreateBlobClients(AzureBlobTtsOptions opts, string contentHash)
    {
        var service = new BlobServiceClient(opts.ConnectionString);
        var container = service.GetBlobContainerClient(opts.ContainerName);
        var prefix = string.IsNullOrWhiteSpace(opts.BlobPrefix) ? "tts" : opts.BlobPrefix.Trim().Trim('/');
        var blobName = $"{prefix}/{contentHash}.mp3";
        return (container, container.GetBlobClient(blobName));
    }

    private async Task<byte[]> CallElevenLabsAsync(
        ElevenLabsTtsOptions el,
        string voiceId,
        string modelId,
        string text,
        string outputFormat,
        ElevenLabsVoiceSettings voiceSettings,
        CancellationToken cancellationToken)
    {
        var baseUrl = el.BaseUrl.Trim().TrimEnd('/');
        var url =
            $"{baseUrl}/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format={Uri.EscapeDataString(outputFormat)}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("xi-api-key", el.ApiKey);

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        };
        req.Content = JsonContent.Create(
            new ElevenLabsTtsBody
            {
                Text = text,
                ModelId = modelId,
                VoiceSettings = voiceSettings
            },
            options: jsonOptions);

        var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("ElevenLabs error {Status}: {Body}", response.StatusCode, err);
            throw new InvalidOperationException($"ElevenLabs request failed: {(int)response.StatusCode}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<byte[]> CallPollyAsync(string voiceId, string engineName, string text, CancellationToken cancellationToken)
    {
        var engine = engineName.Equals("standard", StringComparison.OrdinalIgnoreCase)
            ? Engine.Standard
            : Engine.Neural;

        var synth = new SynthesizeSpeechRequest
        {
            OutputFormat = OutputFormat.Mp3,
            VoiceId = voiceId,
            Text = text,
            Engine = engine
        };

        var response = await _polly.SynthesizeSpeechAsync(synth, cancellationToken);
        await using var stream = response.AudioStream;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private sealed class ElevenLabsTtsBody
    {
        public required string Text { get; set; }
        public required string ModelId { get; set; }
        public required ElevenLabsVoiceSettings VoiceSettings { get; set; }
    }
}
