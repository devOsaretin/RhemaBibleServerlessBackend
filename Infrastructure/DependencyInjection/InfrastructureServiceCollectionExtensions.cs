using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Amazon;
using Amazon.Polly;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Application.Configuration;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Infrastructure.Mongo;
using RhemaBibleAppServerless.Infrastructure.Services.Maintenance;
using RhemaBibleAppServerless.Infrastructure.Persistence;

namespace RhemaBibleAppServerless.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
  public static IServiceCollection AddRhemaInfrastructure(this IServiceCollection services, IConfiguration config)
  {
    MongoEnumStringConvention.RegisterEnumStringConvention();

    services.Configure<MongoDbSettings>(config.GetSection("MongoDbSettings"));
    services.Configure<MongoIndexInitializationOptions>(config.GetSection(MongoIndexInitializationOptions.SectionName));
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
    services.AddSingleton<MongoIndexInitializer>();
    services.AddSingleton<LegacySubscriptionDataMigrationRunner>();

    services.AddScoped<IUserPersistence, MongoUserPersistence>();
    services.AddScoped<INoteRepository, MongoNoteRepository>();
    services.AddScoped<ISavedVerseRepository, MongoSavedVerseRepository>();
    services.AddScoped<IRecentActivityRepository, MongoRecentActivityRepository>();
    services.AddScoped<IOtpRepository, MongoOtpRepository>();
    services.AddScoped<IProcessedWebhookRepository, MongoProcessedWebhookRepository>();
    services.AddScoped<IAdminMetricsRepository, MongoAdminMetricsRepository>();

    services.AddSingleton<IPromptFileReader, CachedPromptFileReader>();

    services.AddHttpClient<MyOpenAIClient>(client =>
    {
      var apiKey = config["OpenAI:ApiKey"];
      if (string.IsNullOrEmpty(apiKey))
        throw new InvalidOperationException("OpenAI API Key not found in configuration.");

      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    });

    services.AddHttpClient<ITextToSpeechService, TextToSpeechService>();

    services.AddScoped<IWebHookService, WebhookService>();
    services.AddScoped<IEmailProvider, SmtpService>();
    services.AddScoped<INotificationService, EmailNotificationService>();

    return services;
  }
}
