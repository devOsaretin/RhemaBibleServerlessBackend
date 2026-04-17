using System.Net.Http.Headers;
using Amazon;
using Amazon.Polly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Application.Configuration;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Infrastructure.Persistence;
using RhemaBibleAppServerless.Infrastructure.Services.Maintenance;

namespace RhemaBibleAppServerless.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
  public static IServiceCollection AddRhemaInfrastructure(this IServiceCollection services, IConfiguration config)
  {
    services.Configure<PostgresOptions>(config.GetSection(PostgresOptions.SectionName));
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

    services.AddDbContext<RhemaDbContext>((sp, o) =>
    {
      var cs = sp.GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString;
      if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("Configure Postgres:ConnectionString (Neon pooled connection string).");

      o.UseNpgsql(cs, n => n.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null))
        .UseSnakeCaseNamingConvention();
    });

    services.AddScoped<PostgresMaintenanceService>();

    services.AddScoped<IUserPersistence, EfUserPersistence>();
    services.AddScoped<INoteRepository, EfNoteRepository>();
    services.AddScoped<ISavedVerseRepository, EfSavedVerseRepository>();
    services.AddScoped<IRecentActivityRepository, EfRecentActivityRepository>();
    services.AddScoped<IOtpRepository, EfOtpRepository>();
    services.AddScoped<IProcessedWebhookRepository, EfProcessedWebhookRepository>();
    services.AddScoped<IAdminMetricsRepository, EfAdminMetricsRepository>();

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
