using Microsoft.Extensions.Logging;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiPlaywrightFullService : IMusicProvider
{
    private readonly AnghamiPlaywrightWriter _writer;
    private readonly ILogger<AnghamiPlaywrightFullService> _logger;

    public AnghamiPlaywrightFullService(
        AnghamiPlaywrightWriter writer,
        ILogger<AnghamiPlaywrightFullService> logger)
    {
        _writer = writer;
        _logger = logger;
    }

    public string ProviderName => "anghami";

    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(string accessToken, CancellationToken ct)
    {
        return await _writer.GetPlaylistsAsync();
    }

    public async Task<IEnumerable<Track>> GetTracksAsync(string accessToken, string playlistId, CancellationToken ct)
    {
        return await _writer.GetPlaylistTracksAsync(playlistId);
    }

    public async Task<string> CreatePlaylistAsync(string accessToken, string name, string? description, CancellationToken ct)
    {
        return await _writer.CreatePlaylistAsync(name, description);
    }

    public async Task AddTracksAsync(string accessToken, string playlistId, IEnumerable<Track> tracks, CancellationToken ct)
    {
        foreach (var track in tracks)
        {
            try
            {
                await _writer.AddTrackToPlaylistAsync(playlistId, track.Id);
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add track {TrackId} ({Title}) to Anghami playlist", track.Id, track.Title);
            }
        }
    }

    public async Task<Track?> SearchTrackAsync(string accessToken, Track sourceTrack, CancellationToken ct)
    {
        return await _writer.SearchTrackAsync(sourceTrack.Title, sourceTrack.Artist);
    }
}
