using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Persistence;

namespace PlaylistSync.Infrastructure.Auth;

public sealed class OAuthService(
    PlaylistSyncDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<OAuthOptions> options,
    IDataProtectionProvider dataProtectionProvider,
    ITokenRefreshService tokenRefreshService) : IOAuthService
{
    private readonly OAuthOptions _options = options.Value;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ConnectedAccountTokens");

    public async Task<string> BuildAuthorizationUrlAsync(OAuthProvider provider, string userId, CancellationToken cancellationToken = default)
    {
        var (state, verifier, challenge) = PkceGenerator.Generate();

        dbContext.OAuthStates.Add(new OAuthState
        {
            Provider = provider.ToString(),
            UserId = userId,
            State = state,
            CodeVerifier = verifier,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var providerOptions = GetProviderOptions(provider);
        var scopes = provider == OAuthProvider.Spotify
            ? "playlist-read-private playlist-modify-private playlist-modify-public"
            : "non-expiring";

        var authUrl = provider == OAuthProvider.Spotify
            ? "https://accounts.spotify.com/authorize"
            : "https://soundcloud.com/connect";

        var query = new Dictionary<string, string>
        {
            ["client_id"] = providerOptions.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = providerOptions.CallbackUrl,
            ["scope"] = scopes,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        return QueryString(query, authUrl);
    }

    public async Task<AuthCallbackResult> HandleCallbackAsync(OAuthProvider provider, string code, string state, string userId, CancellationToken cancellationToken = default)
    {
        var storedState = await dbContext.OAuthStates
            .SingleOrDefaultAsync(x => x.State == state && x.Provider == provider.ToString() && x.UserId == userId, cancellationToken);

        if (storedState is null || storedState.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Invalid or expired state.");
        }

        dbContext.OAuthStates.Remove(storedState);

        var token = await ExchangeCodeAsync(provider, code, storedState.CodeVerifier, cancellationToken);

        var account = await dbContext.ConnectedAccounts
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Provider == provider.ToString(), cancellationToken);

        if (account is null)
        {
            account = new ConnectedAccount
            {
                UserId = userId,
                Provider = provider.ToString()
            };
            dbContext.ConnectedAccounts.Add(account);
        }

        account.EncryptedAccessToken = _protector.Protect(token.AccessToken);
        account.EncryptedRefreshToken = token.RefreshToken is null ? null : _protector.Protect(token.RefreshToken);
        account.ExpiresAt = token.ExpiresIn is null ? null : DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn.Value);
        account.LastRefreshResult = "connected";
        account.LastRefreshedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await tokenRefreshService.RefreshIfExpiringAsync(userId, provider, cancellationToken);
    }

    private async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthProvider provider, string code, string verifier, CancellationToken cancellationToken)
    {
        var providerOptions = GetProviderOptions(provider);
        var tokenUrl = provider == OAuthProvider.Spotify
            ? "https://accounts.spotify.com/api/token"
            : "https://api.soundcloud.com/oauth2/token";

        var clientName = provider == OAuthProvider.Spotify ? "SpotifyClient" : "SoundCloudClient";
        var client = httpClientFactory.CreateClient(clientName);

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = providerOptions.CallbackUrl,
            ["client_id"] = providerOptions.ClientId,
            ["client_secret"] = providerOptions.ClientSecret,
            ["code_verifier"] = verifier
        };

        using var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(formData), cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Token exchange failed.");
        }

        return payload;
    }

    private OAuthProviderOptions GetProviderOptions(OAuthProvider provider)
        => provider == OAuthProvider.Spotify ? _options.Spotify : _options.SoundCloud;

    private static string QueryString(IReadOnlyDictionary<string, string> values, string baseUrl)
    {
        var args = string.Join("&", values.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{baseUrl}?{args}";
    }
}
