using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Interfaces;

public interface IMusicProvider
{
    string ProviderName { get; }

    Task<IEnumerable<Playlist>> GetPlaylistsAsync(string accessToken, CancellationToken ct);

    Task<IEnumerable<Track>> GetTracksAsync(string accessToken, string playlistId, CancellationToken ct);

    Task<string> CreatePlaylistAsync(string accessToken, string name, string? description, CancellationToken ct);

    Task AddTracksAsync(string accessToken, string playlistId, IEnumerable<Track> tracks, CancellationToken ct);

    Task<Track?> SearchTrackAsync(string accessToken, Track sourceTrack, CancellationToken ct);
}
