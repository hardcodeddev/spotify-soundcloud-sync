namespace PlaylistSync.Infrastructure.Persistence;

public sealed class ConnectedAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string? EncryptedRefreshToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string LastRefreshResult { get; set; } = "never";
    public DateTimeOffset? LastRefreshedAt { get; set; }
}
