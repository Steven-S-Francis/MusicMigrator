using System.Text.RegularExpressions;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Services;

public partial class FuzzyTrackMatcher : ITrackMatcher
{
    public async Task<(Track? Match, double Score, MatchStatus Status)> FindBestMatchAsync(
        IMusicProvider targetProvider,
        string accessToken,
        Track sourceTrack,
        CancellationToken ct)
    {
        var candidate = await targetProvider.SearchTrackAsync(accessToken, sourceTrack, ct);

        if (candidate is null)
            return (null, 0.0, MatchStatus.NotFound);

        var score = CalculateConfidence(sourceTrack, candidate);
        var status = ClassifyStatus(score);

        return status == MatchStatus.NotFound
            ? (null, score, MatchStatus.NotFound)
            : (candidate, score, status);
    }

    private static double CalculateConfidence(Track source, Track candidate)
    {
        // ISRC exact match (case-insensitive)
        if (!string.IsNullOrWhiteSpace(source.IsrcCode)
            && !string.IsNullOrWhiteSpace(candidate.IsrcCode)
            && string.Equals(source.IsrcCode, candidate.IsrcCode, StringComparison.OrdinalIgnoreCase))
        {
            return 1.00;
        }

        var srcTitle = Normalize(source.Title);
        var srcArtist = Normalize(source.Artist);
        var candTitle = Normalize(candidate.Title);
        var candArtist = Normalize(candidate.Artist);

        var titlesEqual = srcTitle == candTitle;
        var artistsEqual = srcArtist == candArtist;
        var oneArtistContainsOther = srcArtist.Contains(candArtist, StringComparison.Ordinal)
                                     || candArtist.Contains(srcArtist, StringComparison.Ordinal);
        var oneTitleContainsOther = srcTitle.Contains(candTitle, StringComparison.Ordinal)
                                   || candTitle.Contains(srcTitle, StringComparison.Ordinal);
        var durationsClose = Math.Abs(source.DurationMs - candidate.DurationMs) <= 3000;

        // Normalized title + normalized artist match exactly
        if (titlesEqual && artistsEqual)
            return 0.95;

        // Normalized title matches + one artist name contains the other
        if (titlesEqual && oneArtistContainsOther)
            return 0.85;

        // One title contains the other + one artist contains the other
        if (oneTitleContainsOther && oneArtistContainsOther)
            return 0.75;

        // Durations within 3000ms + one title contains the other
        if (durationsClose && oneTitleContainsOther)
            return 0.65;

        // Title matches exactly, artists differ
        if (titlesEqual)
            return 0.50;

        // Everything else
        return 0.10;
    }

    private static MatchStatus ClassifyStatus(double score) => score switch
    {
        >= 0.80 => MatchStatus.Matched,
        >= 0.50 => MatchStatus.PartialMatch,
        _ => MatchStatus.NotFound
    };

    private static string Normalize(string input)
    {
        // Lowercase
        var result = input.ToLowerInvariant();

        // Strip (feat. ...) / (ft. ...) / (with ...) parenthetical patterns
        result = ParentheticalPattern().Replace(result, string.Empty);

        // Remove all non-word non-space characters
        result = NonWordNonSpacePattern().Replace(result, string.Empty);

        // Collapse multiple spaces to one and trim
        result = MultipleSpacesPattern().Replace(result, " ").Trim();

        return result;
    }

    [GeneratedRegex(@"\s*\((?:feat\.?|ft\.?|with)\s+[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ParentheticalPattern();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex NonWordNonSpacePattern();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpacesPattern();
}
