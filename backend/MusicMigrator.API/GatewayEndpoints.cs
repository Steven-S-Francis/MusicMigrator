using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;
using MusicMigrator.Providers.Anghami;
using MusicMigrator.Providers.YouTube;

namespace MusicMigrator.API;

public record GatewayMigrationRequest(string Cookies, string Sid, string Fingerprint);

public class GatewayPlaylistStatus
{
    public string PlaylistName { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public int MatchedTracks { get; set; }
    public string Status { get; set; } = "Pending";

    public GatewayPlaylistStatus() { }

    public GatewayPlaylistStatus(string playlistName, string playlistId, int totalTracks, int matchedTracks, string status)
    {
        PlaylistName = playlistName;
        PlaylistId = playlistId;
        TotalTracks = totalTracks;
        MatchedTracks = matchedTracks;
        Status = status;
    }
}

public class GatewayMigrationStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public int TotalPlaylists { get; set; }
    public int CompletedPlaylists { get; set; }
    public int TotalTracks { get; set; }
    public int MatchedTracks { get; set; }
    public string? ErrorMessage { get; set; }
    public List<GatewayPlaylistStatus> Playlists { get; set; } = [];

    public GatewayMigrationStatus() { }
}

public static class GatewayEndpoints
{
    private static readonly ConcurrentDictionary<string, GatewayMigrationStatus> _gatewayJobs = new();

    public static void MapGatewayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/migrate/gateway").WithTags("Gateway Migration");

        group.MapPost("/", async (
            HttpContext ctx,
            GatewayMigrationRequest request,
            ITokenStore tokenStore,
            IServiceScopeFactory scopeFactory) =>
        {
            var sessionId = ctx.Session.GetString("session_id");
            if (sessionId is null)
                return Results.Unauthorized();

            var ytToken = tokenStore.Get(sessionId, "youtube");
            if (ytToken is null || ytToken.IsExpired || ytToken.AccessToken is null)
                return Results.Json(new { error = "YouTube not connected. Connect YouTube first." }, statusCode: 400);

            var jobId = Guid.NewGuid().ToString();
            var status = new GatewayMigrationStatus
            {
                JobId = jobId,
                Status = "Running",
                Playlists = []
            };

            _gatewayJobs[jobId] = status;

            _ = Task.Run(async () =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var gateway = scope.ServiceProvider.GetRequiredService<AnghamiGatewayClient>();
                var youtubeService = scope.ServiceProvider.GetRequiredService<YouTubeMusicService>();
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("GatewayMigration");
                var ytAccessToken = tokenStore.Get(sessionId, "youtube")?.AccessToken ?? string.Empty;

                try
                {
                    var playlists = await gateway.GetPlaylistsAsync(
                        request.Cookies, request.Sid, request.Fingerprint, CancellationToken.None);

                    status.TotalPlaylists = playlists.Count;
                    status.Playlists = playlists.Select(p => new GatewayPlaylistStatus(
                        p.Name, p.Id, p.TrackCount, 0, "Pending")).ToList();

                    foreach (var (playlist, idx) in playlists.Select((p, i) => (p, i)))
                    {
                        status.Playlists[idx].Status = "Running";

                        try
                        {
                            var tracks = await gateway.GetPlaylistTracksAsync(
                                playlist.Id, request.Cookies, request.Sid, request.Fingerprint,
                                CancellationToken.None);

                            var ytPlaylistId = await youtubeService.CreatePlaylistAsync(
                                ytAccessToken, playlist.Name, null, CancellationToken.None);

                            var matchedTracks = new List<Track>();
                            foreach (var track in tracks)
                            {
                                try
                                {
                                    var match = await youtubeService.SearchTrackAsync(
                                        ytAccessToken, track, CancellationToken.None);
                                    if (match is not null)
                                        matchedTracks.Add(match);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to search/match track {Title} - {Artist}",
                                        track.Title, track.Artist);
                                }

                                await Task.Delay(150, CancellationToken.None);
                            }

                            if (matchedTracks.Count > 0)
                            {
                                await youtubeService.AddTracksAsync(
                                    ytAccessToken, ytPlaylistId, matchedTracks, CancellationToken.None);
                            }

                            status.Playlists[idx].Status = "Completed";
                            status.Playlists[idx].MatchedTracks = matchedTracks.Count;
                            status.TotalTracks += tracks.Count;
                            status.MatchedTracks += matchedTracks.Count;
                            status.CompletedPlaylists++;

                            logger.LogInformation(
                                "Gateway migration: playlist '{Name}': {Matched}/{Total} tracks matched",
                                playlist.Name, matchedTracks.Count, tracks.Count);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Gateway migration failed for playlist '{Name}'", playlist.Name);
                            status.Playlists[idx].Status = "Failed";
                            status.CompletedPlaylists++;
                        }
                    }

                    status.Status = "Completed";
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Gateway migration failed");
                    status.Status = "Failed";
                    status.ErrorMessage = ex.Message;
                }
            });

            return Results.Accepted($"/migrate/gateway/{jobId}", new { jobId });
        });

        group.MapGet("/{jobId}", (string jobId) =>
        {
            if (!_gatewayJobs.TryGetValue(jobId, out var status))
                return Results.NotFound();
            return Results.Ok(status);
        });
    }
}
