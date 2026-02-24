using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Auth;
using PlaylistSync.Infrastructure.Persistence;
using PlaylistSync.Infrastructure.Providers;

namespace PlaylistSync.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaylistSyncInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));

        services.AddDbContext<PlaylistSyncDbContext>(options =>
            options.UseInMemoryDatabase("PlaylistSync"));

        services.AddHttpClient("SpotifyClient");
        services.AddHttpClient("SoundCloudClient");

        services.AddScoped<ITrackMatchingStrategy, TrackMatchingStrategy>();
        services.AddScoped<SpotifyProviderClient>();
        services.AddScoped<SoundCloudProviderClient>();
        services.AddScoped<IReadOnlyDictionary<string, ISourceMusicProvider>>(sp =>
            new Dictionary<string, ISourceMusicProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["spotify"] = sp.GetRequiredService<SpotifyProviderClient>(),
                ["soundcloud"] = sp.GetRequiredService<SoundCloudProviderClient>()
            });

        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<ITokenRefreshService, TokenRefreshService>();

        return services;
    }
}
