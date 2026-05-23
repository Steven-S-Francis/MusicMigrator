namespace MusicMigrator.Core.Models;

public record Track(
    string Id,
    string Title,
    string Artist,
    string? Album,
    int DurationMs,
    string? IsrcCode);
