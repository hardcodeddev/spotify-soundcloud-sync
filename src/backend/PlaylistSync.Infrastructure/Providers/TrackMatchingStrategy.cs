using Microsoft.Extensions.Logging;
using PlaylistSync.Core;

namespace PlaylistSync.Infrastructure.Providers;

public interface ITrackMatchingStrategy
{
    NormalizedTrack? FindBestMatch(NormalizedTrack source, IReadOnlyList<NormalizedTrack> candidates);
}

public sealed class TrackMatchingStrategy(ILogger<TrackMatchingStrategy> logger) : ITrackMatchingStrategy
{
    private const double FuzzyThreshold = 0.80;

    public NormalizedTrack? FindBestMatch(NormalizedTrack source, IReadOnlyList<NormalizedTrack> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var isrc = source.Isrc?.Trim();
        if (!string.IsNullOrWhiteSpace(isrc))
        {
            var byIsrc = candidates.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.Isrc) &&
                string.Equals(c.Isrc, isrc, StringComparison.OrdinalIgnoreCase));
            if (byIsrc is not null)
            {
                return byIsrc;
            }
        }

        var normalizedTitle = Normalize(source.Title);
        var normalizedArtist = Normalize(source.Artists.FirstOrDefault() ?? string.Empty);

        var exactNormalized = candidates.FirstOrDefault(c =>
            Normalize(c.Title) == normalizedTitle &&
            Normalize(c.Artists.FirstOrDefault() ?? string.Empty) == normalizedArtist);

        if (exactNormalized is not null)
        {
            return exactNormalized;
        }

        var scoredCandidates = candidates
            .Select(c => new
            {
                Candidate = c,
                Score = (Similarity(normalizedTitle, Normalize(c.Title)) * 0.7)
                      + (Similarity(normalizedArtist, Normalize(c.Artists.FirstOrDefault() ?? string.Empty)) * 0.3)
            })
            .OrderByDescending(x => x.Score)
            .ToArray();

        var best = scoredCandidates.FirstOrDefault();
        if (best is null || best.Score < FuzzyThreshold)
        {
            logger.LogInformation(
                "No track match met fuzzy threshold for '{Title}' by '{Artist}'. Best confidence: {Confidence}",
                source.Title,
                source.Artists.FirstOrDefault() ?? "unknown",
                best?.Score ?? 0);
            return null;
        }

        logger.LogInformation(
            "Fuzzy matched '{Title}' by '{Artist}' with confidence {Confidence}",
            source.Title,
            source.Artists.FirstOrDefault() ?? "unknown",
            best.Score);

        return best.Candidate;
    }

    private static string Normalize(string value)
    {
        var cleaned = new string(value
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray());

        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 && right.Length == 0)
        {
            return 1;
        }

        var distance = LevenshteinDistance(left, right);
        var maxLength = Math.Max(left.Length, right.Length);
        return maxLength == 0 ? 1 : 1 - ((double)distance / maxLength);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var rows = left.Length + 1;
        var cols = right.Length + 1;
        var matrix = new int[rows, cols];

        for (var i = 0; i < rows; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j < cols; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }
}
