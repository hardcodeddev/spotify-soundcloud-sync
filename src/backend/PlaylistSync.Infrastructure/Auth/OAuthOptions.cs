namespace PlaylistSync.Infrastructure.Auth;

public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";
    public OAuthProviderOptions Spotify { get; set; } = new();
    public OAuthProviderOptions SoundCloud { get; set; } = new();
}

public sealed class OAuthProviderOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}
