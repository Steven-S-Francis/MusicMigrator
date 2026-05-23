using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Interfaces;

public interface ITrackMatcher
{
    Task<(Track? Match, double Score, MatchStatus Status)> FindBestMatchAsync(
        IMusicProvider targetProvider,
        string accessToken,
        Track sourceTrack,
        CancellationToken ct);
}
