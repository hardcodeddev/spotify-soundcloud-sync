using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PlaylistSync.Core;

namespace PlaylistSync.Infrastructure.Providers;

public sealed class SpotifyProviderClient(
    IHttpClientFactory httpClientFactory,
    ITrackMatchingStrategy trackMatchingStrategy) : ISourceMusicProvider
{
    public string ProviderName => "spotify";

    public async Task<IReadOnlyList<NormalizedTrack>> FetchLikedTracksAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(accessToken, HttpMethod.Get, "me/tracks?limit=50", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => ToNormalizedTrack(item.GetProperty("track")))
            .ToArray();
    }

    public async Task<IReadOnlyList<NormalizedPlaylist>> FetchPlaylistsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(accessToken, HttpMethod.Get, "me/playlists?limit=50", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => new NormalizedPlaylist(
                item.GetProperty("id").GetString() ?? string.Empty,
                item.GetProperty("name").GetString() ?? string.Empty,
                item.TryGetProperty("description", out var description) ? description.GetString() : null,
                item.TryGetProperty("public", out var isPublic) && isPublic.GetBoolean(),
                item.GetProperty("external_urls").GetProperty("spotify").GetString() ?? string.Empty))
            .ToArray();
    }

    public async Task<IReadOnlyList<NormalizedTrack>> FetchPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(accessToken, HttpMethod.Get, $"playlists/{playlistId}/tracks?limit=100", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Where(x => x.TryGetProperty("track", out _))
            .Select(item => ToNormalizedTrack(item.GetProperty("track")))
            .ToArray();
    }

    public async Task<string> CreateOrUpdatePlaylistAsync(string accessToken, string playlistName, string? description, bool isPublic, CancellationToken cancellationToken = default)
    {
        var playlists = await FetchPlaylistsAsync(accessToken, cancellationToken);
        var existing = playlists.FirstOrDefault(x => string.Equals(x.Name, playlistName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            var body = JsonSerializer.Serialize(new { name = playlistName, description, @public = isPublic });
            await SendAsync(accessToken, HttpMethod.Put, $"playlists/{existing.Id}", cancellationToken, body);
            return existing.Id;
        }

        var meResponse = await SendAsync(accessToken, HttpMethod.Get, "me", cancellationToken);
        var mePayload = await meResponse.Content.ReadAsStringAsync(cancellationToken);
        using var meDoc = JsonDocument.Parse(mePayload);
        var userId = meDoc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Spotify user id missing.");

        var createBody = JsonSerializer.Serialize(new { name = playlistName, description, @public = isPublic });
        var response = await SendAsync(accessToken, HttpMethod.Post, $"users/{userId}/playlists", cancellationToken, createBody);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Spotify playlist id missing.");
    }

    public async Task AddTracksToPlaylistAsync(string accessToken, string playlistId, IReadOnlyList<NormalizedTrack> tracks, CancellationToken cancellationToken = default)
    {
        var trackIds = await ResolveSpotifyTrackIdsAsync(accessToken, tracks, cancellationToken);
        if (trackIds.Count == 0)
        {
            return;
        }

        var uris = trackIds.Select(id => $"spotify:track:{id}").ToArray();
        var body = JsonSerializer.Serialize(new { uris });

        await SendAsync(accessToken, HttpMethod.Post, $"playlists/{playlistId}/tracks", cancellationToken, body);
    }

    public async Task LikeTracksAsync(string accessToken, IReadOnlyList<NormalizedTrack> tracks, CancellationToken cancellationToken = default)
    {
        var trackIds = await ResolveSpotifyTrackIdsAsync(accessToken, tracks, cancellationToken);
        if (trackIds.Count == 0)
        {
            return;
        }

        await SendAsync(accessToken, HttpMethod.Put, $"me/tracks?ids={string.Join(',', trackIds)}", cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ResolveSpotifyTrackIdsAsync(string accessToken, IReadOnlyList<NormalizedTrack> tracks, CancellationToken cancellationToken)
    {
        var ids = new List<string>();

        foreach (var track in tracks)
        {
            if (track.ExternalIds.TryGetValue("spotifyId", out var spotifyId) && !string.IsNullOrWhiteSpace(spotifyId))
            {
                ids.Add(spotifyId);
                continue;
            }

            var candidates = await SearchCandidatesAsync(accessToken, track, cancellationToken);
            var best = trackMatchingStrategy.FindBestMatch(track, candidates);
            if (best?.ExternalIds.TryGetValue("spotifyId", out var resolved) == true)
            {
                ids.Add(resolved);
            }
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<IReadOnlyList<NormalizedTrack>> SearchCandidatesAsync(string accessToken, NormalizedTrack track, CancellationToken cancellationToken)
    {
        var query = !string.IsNullOrWhiteSpace(track.Isrc)
            ? $"isrc:{track.Isrc}"
            : $"track:{track.Title} artist:{track.Artists.FirstOrDefault() ?? string.Empty}";

        var response = await SendAsync(accessToken, HttpMethod.Get, $"search?type=track&limit=10&q={Uri.EscapeDataString(query)}", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("tracks", out var tracksRoot) || !tracksRoot.TryGetProperty("items", out var items))
        {
            return [];
        }

        return items.EnumerateArray().Select(ToNormalizedTrack).ToArray();
    }

    private static NormalizedTrack ToNormalizedTrack(JsonElement track)
    {
        var externalIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var id = track.GetProperty("id").GetString();
        if (!string.IsNullOrWhiteSpace(id))
        {
            externalIds["spotifyId"] = id;
        }

        if (track.TryGetProperty("external_ids", out var ext) && ext.TryGetProperty("isrc", out var isrcFromMap))
        {
            var isrcValue = isrcFromMap.GetString();
            if (!string.IsNullOrWhiteSpace(isrcValue))
            {
                externalIds["isrc"] = isrcValue;
            }
        }

        var artists = track.GetProperty("artists")
            .EnumerateArray()
            .Select(x => x.GetProperty("name").GetString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return new NormalizedTrack(
            track.GetProperty("name").GetString() ?? string.Empty,
            artists,
            track.TryGetProperty("duration_ms", out var duration) ? duration.GetInt32() : 0,
            externalIds.TryGetValue("isrc", out var isrc) ? isrc : null,
            externalIds,
            track.GetProperty("external_urls").GetProperty("spotify").GetString() ?? string.Empty);
    }

    private async Task<HttpResponseMessage> SendAsync(string accessToken, HttpMethod method, string relativePath, CancellationToken cancellationToken, string? jsonBody = null)
    {
        var client = httpClientFactory.CreateClient("SpotifyClient");
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri("https://api.spotify.com/v1/");
        }

        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
