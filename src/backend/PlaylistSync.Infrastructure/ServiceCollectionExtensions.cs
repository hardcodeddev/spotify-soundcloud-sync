using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlaylistSync.Core;

namespace PlaylistSync.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaylistSyncInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("SpotifyClient");
        services.AddHttpClient("SoundCloudClient");
        services.AddScoped<ISyncService, SyncService>();

        return services;
    }
}
