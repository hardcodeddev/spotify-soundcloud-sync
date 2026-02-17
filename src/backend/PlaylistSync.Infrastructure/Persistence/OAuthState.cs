namespace PlaylistSync.Infrastructure.Persistence;

public sealed class OAuthState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
