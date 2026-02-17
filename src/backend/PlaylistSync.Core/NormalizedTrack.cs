namespace PlaylistSync.Core;

public sealed record NormalizedTrack(
    string Title,
    IReadOnlyList<string> Artists,
    int DurationMs,
    string? Isrc,
    IReadOnlyDictionary<string, string> ExternalIds,
    string SourceUrl);

public sealed record NormalizedPlaylist(
    string Id,
    string Name,
    string? Description,
    bool IsPublic,
    string SourceUrl);
