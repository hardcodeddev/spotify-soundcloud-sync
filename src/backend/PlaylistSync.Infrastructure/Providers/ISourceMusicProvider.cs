using PlaylistSync.Core;

namespace PlaylistSync.Infrastructure.Providers;

public interface ISourceMusicProvider
{
    string ProviderName { get; }

    Task<IReadOnlyList<NormalizedTrack>> FetchLikedTracksAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NormalizedPlaylist>> FetchPlaylistsAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NormalizedTrack>> FetchPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken cancellationToken = default);

    Task<string> CreateOrUpdatePlaylistAsync(
        string accessToken,
        string playlistName,
        string? description,
        bool isPublic,
        CancellationToken cancellationToken = default);

    Task AddTracksToPlaylistAsync(
        string accessToken,
        string playlistId,
        IReadOnlyList<NormalizedTrack> tracks,
        CancellationToken cancellationToken = default);

    Task LikeTracksAsync(
        string accessToken,
        IReadOnlyList<NormalizedTrack> tracks,
        CancellationToken cancellationToken = default);
}
