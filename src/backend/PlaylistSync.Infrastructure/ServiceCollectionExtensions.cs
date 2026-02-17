using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Auth;
using PlaylistSync.Infrastructure.Persistence;

namespace PlaylistSync.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaylistSyncInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));

        services.AddDbContext<PlaylistSyncDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PlaylistSyncDb")));

        services.AddHttpClient("SpotifyClient");
        services.AddHttpClient("SoundCloudClient");

        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<ITokenRefreshService, TokenRefreshService>();

        return services;
    }
}
