using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PlaylistSync.Core;

namespace PlaylistSync.Infrastructure.Providers;

public sealed class SoundCloudProviderClient(
    IHttpClientFactory httpClientFactory,
    ITrackMatchingStrategy trackMatchingStrategy) : ISourceMusicProvider
{
    public string ProviderName => "soundcloud";

    public async Task<IReadOnlyList<NormalizedTrack>> FetchLikedTracksAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(accessToken, HttpMethod.Get, "me/likes?limit=200", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("collection")
            .EnumerateArray()
            .Where(x => x.TryGetProperty("track", out _))
            .Select(item => ToNormalizedTrack(item.GetProperty("track")))
            .ToArray();
    }

    public async Task<IReadOnlyList<NormalizedPlaylist>> FetchPlaylistsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(accessToken, HttpMethod.Get, "me/playlists?limit=200", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("collection")
            .EnumerateArray()
            .Select(item => new NormalizedPlaylist(
                item.GetProperty("id").GetInt64().ToString(),
                item.GetProperty("title").GetString() ?? string.Empty,
                item.TryGetProperty("description", out var description) ? description.GetString() : null,
                item.TryGetProperty("sharing", out var sharing) && string.Equals(sharing.GetString(), "public", StringComparison.OrdinalIgnoreCase),
                item.TryGetProperty("permalink_url", out var sourceUrl) ? sourceUrl.GetString() ?? string.Empty : string.Empty))
            .ToArray();
    }

    public async Task<IReadOnlyList<NormalizedTrack>> FetchPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(accessToken, HttpMethod.Get, $"playlists/{playlistId}", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("tracks")
            .EnumerateArray()
            .Select(ToNormalizedTrack)
            .ToArray();
    }

    public async Task<string> CreateOrUpdatePlaylistAsync(string accessToken, string playlistName, string? description, bool isPublic, CancellationToken cancellationToken = default)
    {
        var existing = (await FetchPlaylistsAsync(accessToken, cancellationToken))
            .FirstOrDefault(x => string.Equals(x.Name, playlistName, StringComparison.OrdinalIgnoreCase));

        var payload = JsonSerializer.Serialize(new
        {
            playlist = new
            {
                title = playlistName,
                description,
                sharing = isPublic ? "public" : "private"
            }
        });

        if (existing is not null)
        {
            await SendAsync(accessToken, HttpMethod.Put, $"playlists/{existing.Id}", cancellationToken, payload);
            return existing.Id;
        }

        var response = await SendAsync(accessToken, HttpMethod.Post, "playlists", cancellationToken, payload);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("id").GetInt64().ToString();
    }

    public async Task AddTracksToPlaylistAsync(string accessToken, string playlistId, IReadOnlyList<NormalizedTrack> tracks, CancellationToken cancellationToken = default)
    {
        var existing = await FetchPlaylistTracksAsync(accessToken, playlistId, cancellationToken);
        var ids = await ResolveTrackIdsAsync(accessToken, tracks.Concat(existing).ToArray(), cancellationToken);

        var body = JsonSerializer.Serialize(new
        {
            playlist = new
            {
                tracks = ids.Select(x => new { id = long.Parse(x) }).ToArray()
            }
        });

        await SendAsync(accessToken, HttpMethod.Put, $"playlists/{playlistId}", cancellationToken, body);
    }

    public async Task LikeTracksAsync(string accessToken, IReadOnlyList<NormalizedTrack> tracks, CancellationToken cancellationToken = default)
    {
        var ids = await ResolveTrackIdsAsync(accessToken, tracks, cancellationToken);
        foreach (var id in ids)
        {
            await SendAsync(accessToken, HttpMethod.Post, $"likes/tracks/{id}", cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> ResolveTrackIdsAsync(string accessToken, IReadOnlyList<NormalizedTrack> tracks, CancellationToken cancellationToken)
    {
        var ids = new List<string>();

        foreach (var track in tracks)
        {
            if (track.ExternalIds.TryGetValue("soundcloudId", out var existingId) && !string.IsNullOrWhiteSpace(existingId))
            {
                ids.Add(existingId);
                continue;
            }

            var candidates = await SearchCandidatesAsync(accessToken, track, cancellationToken);
            var best = trackMatchingStrategy.FindBestMatch(track, candidates);
            if (best?.ExternalIds.TryGetValue("soundcloudId", out var resolvedId) == true)
            {
                ids.Add(resolvedId);
            }
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<IReadOnlyList<NormalizedTrack>> SearchCandidatesAsync(string accessToken, NormalizedTrack track, CancellationToken cancellationToken)
    {
        var q = $"{track.Title} {track.Artists.FirstOrDefault() ?? string.Empty}";
        var response = await SendAsync(accessToken, HttpMethod.Get, $"tracks?limit=25&q={Uri.EscapeDataString(q)}", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(ToNormalizedTrack).ToArray();
        }

        if (root.TryGetProperty("collection", out var collection))
        {
            return collection.EnumerateArray().Select(ToNormalizedTrack).ToArray();
        }

        return [];
    }

    private static NormalizedTrack ToNormalizedTrack(JsonElement track)
    {
        var externalIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (track.TryGetProperty("id", out var idNode))
        {
            externalIds["soundcloudId"] = idNode.GetInt64().ToString();
        }

        var title = track.TryGetProperty("title", out var titleNode) ? titleNode.GetString() ?? string.Empty : string.Empty;
        var artist = track.TryGetProperty("user", out var user) && user.TryGetProperty("username", out var username)
            ? username.GetString() ?? string.Empty
            : string.Empty;
        var durationMs = track.TryGetProperty("duration", out var duration) ? duration.GetInt32() : 0;
        var sourceUrl = track.TryGetProperty("permalink_url", out var permalink) ? permalink.GetString() ?? string.Empty : string.Empty;

        return new NormalizedTrack(title, [artist], durationMs, null, externalIds, sourceUrl);
    }

    private async Task<HttpResponseMessage> SendAsync(string accessToken, HttpMethod method, string relativePath, CancellationToken cancellationToken, string? jsonBody = null)
    {
        var client = httpClientFactory.CreateClient("SoundCloudClient");
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri("https://api-v2.soundcloud.com/");
        }

        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
