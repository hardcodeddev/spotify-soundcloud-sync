namespace PlaylistSync.Core;

public enum OAuthProvider
{
    Spotify,
    SoundCloud
}

public sealed record AuthCallbackResult(
    bool Connected,
    DateTimeOffset? ExpiresAt,
    string LastRefreshResult);

public interface IOAuthService
{
    Task<string> BuildAuthorizationUrlAsync(OAuthProvider provider, string userId, CancellationToken cancellationToken = default);
    Task<AuthCallbackResult> HandleCallbackAsync(
        OAuthProvider provider,
        string code,
        string state,
        string userId,
        CancellationToken cancellationToken = default);
}

public interface ITokenRefreshService
{
    Task<AuthCallbackResult> RefreshIfExpiringAsync(string userId, OAuthProvider provider, CancellationToken cancellationToken = default);
}
