namespace MusicMigrator.Core.Models;

public record Playlist(
    string Id,
    string Name,
    string? Description,
    int TrackCount,
    string? CoverUrl);
