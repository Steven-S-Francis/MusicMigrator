using Microsoft.Extensions.Logging;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiService : IMusicProvider
{
    private readonly AnghamiApiClient _apiClient;
    private readonly AnghamiPlaywrightWriter _playwrightWriter;
    private readonly ILogger<AnghamiService> _logger;

    public AnghamiService(
        AnghamiApiClient apiClient,
        AnghamiPlaywrightWriter playwrightWriter,
        ILogger<AnghamiService> logger)
    {
        _apiClient = apiClient;
        _playwrightWriter = playwrightWriter;
        _logger = logger;
    }

    public string ProviderName => "anghami";

    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(string accessToken, CancellationToken ct)
    {
        return await _apiClient.GetUserPlaylistsAsync(accessToken, ct);
    }

    public async Task<IEnumerable<Track>> GetTracksAsync(string accessToken, string playlistId, CancellationToken ct)
    {
        return await _apiClient.GetPlaylistTracksAsync(accessToken, playlistId, ct);
    }

    public async Task<string> CreatePlaylistAsync(string accessToken, string name, string? description, CancellationToken ct)
    {
        return await _playwrightWriter.CreatePlaylistAsync(accessToken, name, description);
    }

    public async Task AddTracksAsync(string accessToken, string playlistId, IEnumerable<Track> tracks, CancellationToken ct)
    {
        foreach (var track in tracks)
        {
            try
            {
                await _playwrightWriter.AddTrackToPlaylistAsync(accessToken, playlistId, track.Id);
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
        var query = $"{sourceTrack.Title} {sourceTrack.Artist}";
        var results = await _apiClient.SearchTracksAsync(accessToken, query, "SA", ct);
        return results.FirstOrDefault();
    }
}
