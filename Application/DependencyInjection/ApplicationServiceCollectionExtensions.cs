using Microsoft.Extensions.DependencyInjection;

namespace RhemaBibleAppServerless.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
  public static IServiceCollection AddRhemaApplication(this IServiceCollection services)
  {
    services.AddSingleton<IUserResourceEpochStore, UserResourceEpochStore>();

    services.AddScoped<IUserApplicationService, UserApplicationService>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IOtpService, OtpService>();
    services.AddScoped<IAiQuotaService, AiQuotaService>();
    services.AddScoped<INoteService, NoteService>();
    services.AddScoped<ISavedVerseService, SavedVerseService>();
    services.AddScoped<IRecentActivityService, RecentActivityService>();
    services.AddScoped<IAdminService, AdminService>();
    services.AddScoped<IJwtService, JwtService>();
    services.AddScoped<IPasswordHasher, PasswordHasher>();
    services.AddScoped<ICurrentUserService, CurrentUserService>();
    services.AddScoped<IAccountDeletionService, AccountDeletionService>();
    services.AddSingleton<IUrlService, UrlService>();

    return services;
  }
}
