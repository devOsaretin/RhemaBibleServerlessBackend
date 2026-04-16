using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.Polly;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class ApplicationServiceExtension
{
  public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
  {
    services.AddHttpContextAccessor();
    services.AddMemoryCache();
    services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    {
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    services.Configure<JsonOptions>(options =>
    {
      options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    MongoEnumStringConvention.RegisterEnumStringConvention();

    services.Configure<MongoDbSettings>(config.GetSection("MongoDbSettings"));
    services.Configure<RevenueCatSettings>(config.GetSection("RevenueCat"));
    services.Configure<ClerkSettings>(config.GetSection("Clerk"));
    services.Configure<ElevenLabsTtsOptions>(config.GetSection(ElevenLabsTtsOptions.SectionName));
    services.Configure<AzureBlobTtsOptions>(config.GetSection(AzureBlobTtsOptions.SectionName));
    services.Configure<TextToSpeechRoutingOptions>(config.GetSection(TextToSpeechRoutingOptions.SectionName));
    services.Configure<PollyTtsOptions>(config.GetSection(PollyTtsOptions.SectionName));
    services.Configure<AiQueryCacheOptions>(config.GetSection(AiQueryCacheOptions.SectionName));
    services.Configure<AiQuotaOptions>(config.GetSection(AiQuotaOptions.SectionName));

    services.AddSingleton<IAmazonPolly>(sp =>
    {
      var options = sp.GetRequiredService<IOptions<PollyTtsOptions>>().Value;
      var region = RegionEndpoint.GetBySystemName(string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region);
      if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
        return new AmazonPollyClient(options.AccessKey, options.SecretKey, region);
      return new AmazonPollyClient(region);
    });

    services.AddSingleton<MongoDbService>();
    services.AddSingleton<IMongoDbService>(sp => sp.GetRequiredService<MongoDbService>());
    services.AddSingleton<IUserResourceEpochStore, UserResourceEpochStore>();
    services.AddSingleton<IPromptFileReader, CachedPromptFileReader>();

    services.AddHttpClient<MyOpenAIClient>(client =>
    {
      var apiKey = config["OpenAI:ApiKey"];
      if (string.IsNullOrEmpty(apiKey))
        throw new InvalidOperationException("OpenAI API Key not found in configuration.");

      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    });

    services.AddHttpClient<ITextToSpeechService, TextToSpeechService>();

    services.AddScoped<IUserService, UserService>();
    services.AddScoped<IWebHookService, WebhookService>();
    services.AddScoped<ICurrentUserService, CurrentUserService>();
    services.AddScoped<IAiQuotaService, AiQuotaService>();
    services.AddScoped<ISavedVerseService, SavedVerseService>();
    services.AddScoped<INoteService, NoteService>();
    services.AddScoped<IRecentActivityService, RecentActivityService>();
    services.AddScoped<IAdminService, AdminService>();
    services.AddScoped<IJwtService, JwtService>();
    services.AddScoped<IPasswordHasher, PasswordHasher>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IOtpService, OtpService>();
    services.AddScoped<ISubscriptionExpirationService, SubscriptionExpirationService>();
    services.AddScoped<IEmailProvider, SmtpService>();
    services.AddScoped<INotificationService, EmailNotificationService>();

    services.AddScoped<IAIClient>(provider =>
    {
      var currentUserService = provider.GetRequiredService<ICurrentUserService>();
      var recentActivityService = provider.GetRequiredService<IRecentActivityService>();
      var aiQuotaService = provider.GetRequiredService<IAiQuotaService>();
      var promptFiles = provider.GetRequiredService<IPromptFileReader>();
      var memoryCache = provider.GetRequiredService<IMemoryCache>();
      var queryCacheOptions = provider.GetRequiredService<IOptionsMonitor<AiQueryCacheOptions>>();
      var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
      var httpClient = httpClientFactory.CreateClient(nameof(MyOpenAIClient));

      return new MyOpenAIClient(
        currentUserService,
        recentActivityService,
        aiQuotaService,
        promptFiles,
        memoryCache,
        queryCacheOptions,
        httpClient,
        "gpt-4.1-mini");
    });

    services.AddHostedService<SubscriptionExpirationBackgroundService>();
    services.AddHostedService<LegacySubscriptionDataMigrationHostedService>();

    return services;
  }
}

