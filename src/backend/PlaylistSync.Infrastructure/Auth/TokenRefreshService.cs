using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Persistence;

namespace PlaylistSync.Infrastructure.Auth;

public sealed class TokenRefreshService(
    PlaylistSyncDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<OAuthOptions> options,
    IDataProtectionProvider dataProtectionProvider) : ITokenRefreshService
{
    private readonly OAuthOptions _options = options.Value;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ConnectedAccountTokens");

    public async Task<AuthCallbackResult> RefreshIfExpiringAsync(string userId, OAuthProvider provider, CancellationToken cancellationToken = default)
    {
        var account = await dbContext.ConnectedAccounts
            .Include(x => x.UserAccount)
            .SingleOrDefaultAsync(x => x.UserAccount.ExternalUserId == userId && x.Provider == provider.ToString(), cancellationToken);

        if (account is null)
        {
            return new AuthCallbackResult(false, null, "not_connected");
        }

        var expiresAt = account.ExpiresAt;
        if (expiresAt is null || expiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return new AuthCallbackResult(true, expiresAt, account.LastRefreshResult);
        }

        if (string.IsNullOrWhiteSpace(account.RefreshTokenRef))
        {
            account.LastRefreshResult = "refresh_unavailable";
            account.LastRefreshedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new AuthCallbackResult(true, account.ExpiresAt, account.LastRefreshResult);
        }

        var refreshToken = _protector.Unprotect(account.RefreshTokenRef);
        var refreshed = await RefreshTokenAsync(provider, refreshToken, cancellationToken);

        account.AccessTokenRef = _protector.Protect(refreshed.AccessToken);
        if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
        {
            account.RefreshTokenRef = _protector.Protect(refreshed.RefreshToken);
        }

        account.ExpiresAt = refreshed.ExpiresIn is null ? account.ExpiresAt : DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn.Value);
        account.LastRefreshResult = "refreshed";
        account.LastRefreshedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthCallbackResult(true, account.ExpiresAt, account.LastRefreshResult);
    }

    private async Task<OAuthTokenResponse> RefreshTokenAsync(OAuthProvider provider, string refreshToken, CancellationToken cancellationToken)
    {
        var providerOptions = provider == OAuthProvider.Spotify ? _options.Spotify : _options.SoundCloud;
        var tokenUrl = provider == OAuthProvider.Spotify
            ? "https://accounts.spotify.com/api/token"
            : "https://api.soundcloud.com/oauth2/token";

        var client = httpClientFactory.CreateClient(provider == OAuthProvider.Spotify ? "SpotifyClient" : "SoundCloudClient");

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = providerOptions.ClientId,
            ["client_secret"] = providerOptions.ClientSecret
        };

        using var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(formData), cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Refresh failed.");
        }

        return payload;
    }
}
