namespace MusicMigrator.Core.Models;

public record TrackMigrationResult(
    Track SourceTrack,
    Track? MatchedTrack,
    MatchStatus Status,
    double ConfidenceScore,
    string? FailReason);
