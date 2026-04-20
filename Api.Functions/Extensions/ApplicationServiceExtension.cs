using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RhemaBibleAppServerless.Application.Configuration;
using RhemaBibleAppServerless.Application.DependencyInjection;
using RhemaBibleAppServerless.Application.Persistence;
using RhemaBibleAppServerless.Infrastructure.DependencyInjection;

public static class ApplicationServiceExtension
{
  public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
  {
    services.AddMemoryCache();
    services.AddSingleton<ICurrentPrincipalAccessor, AsyncLocalCurrentPrincipalAccessor>();

    services.AddRhemaInfrastructure(config);
    services.AddRhemaApplication();

    services.AddScoped<IAIClient>(provider =>
    {
      var currentUserService = provider.GetRequiredService<ICurrentUserService>();
      var userPersistence = provider.GetRequiredService<IUserPersistence>();
      var recentActivityService = provider.GetRequiredService<IRecentActivityService>();
      var aiQuotaService = provider.GetRequiredService<IAiQuotaService>();
      var promptFiles = provider.GetRequiredService<IPromptFileReader>();
      var memoryCache = provider.GetRequiredService<IMemoryCache>();
      var queryCacheOptions = provider.GetRequiredService<IOptionsMonitor<AiQueryCacheOptions>>();
      var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
      var httpClient = httpClientFactory.CreateClient(nameof(MyOpenAIClient));
      var serviceBusService = provider.GetRequiredService<IServiceBusService>();

      return new MyOpenAIClient(
        currentUserService,
        userPersistence,
        serviceBusService,
        aiQuotaService,
        promptFiles,
        memoryCache,
        queryCacheOptions,
        httpClient,
        "gpt-4.1-mini");
    });

    return services;
  }
}
