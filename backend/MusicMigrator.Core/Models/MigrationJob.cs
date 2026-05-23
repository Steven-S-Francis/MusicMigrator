namespace MusicMigrator.Core.Models;

public class MigrationJob
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string SourceProvider { get; set; } = string.Empty;
    public string DestinationProvider { get; set; } = string.Empty;
    public string SourcePlaylistId { get; set; } = string.Empty;
    public string SourcePlaylistName { get; set; } = string.Empty;
    public MigrationStatus Status { get; set; } = MigrationStatus.Pending;
    public List<TrackMigrationResult> Results { get; } = [];
    public int TotalTracks { get; set; }
    public int ProcessedTracks => Results.Count;
    public string? DestinationPlaylistId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
