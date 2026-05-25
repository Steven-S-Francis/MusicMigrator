using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;
using MusicMigrator.Core.Services;
using MusicMigrator.Providers.Spotify;
using MusicMigrator.Providers.YouTube;

namespace MusicMigrator.API;

public record StartMigrationRequest(
    string SourceProvider,
    string DestinationProvider,
    string PlaylistId,
    string PlaylistName);

public static class MigrationEndpoints
{
    public static void MapMigrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/migrate").WithTags("Migration");

        group.MapPost("/", async (
            HttpContext ctx,
            StartMigrationRequest request,
            IMigrationJobStore jobStore,
            ITokenStore tokenStore,
            SpotifyAuthHandler spotifyAuth,
            YouTubeAuthHandler youtubeAuth,
            IServiceScopeFactory scopeFactory) =>
        {
            var sessionId = ctx.Session.GetString("session_id");
            if (sessionId is null)
                return Results.Unauthorized();

            // Refresh both source and destination tokens before starting the background job
            foreach (var provider in new[] { request.SourceProvider, request.DestinationProvider })
            {
                var token = tokenStore.Get(sessionId, provider);
                if (token is null)
                    return Results.Unauthorized();

                if (token.IsExpired && token.RefreshToken is not null)
                {
                    var (newAccess, newRefresh, newExpires) = provider.ToLowerInvariant() switch
                    {
                        "spotify" => await spotifyAuth.RefreshAsync(token.RefreshToken),
                        "youtube" => await youtubeAuth.RefreshAsync(token.RefreshToken),
                        _ => (null, null, DateTime.MinValue)
                    };

                    if (newAccess is not null)
                        tokenStore.Store(sessionId, provider,
                            new ProviderToken(newAccess, newRefresh, newExpires));
                }
            }

            var job = jobStore.Create(
                sessionId,
                request.SourceProvider,
                request.DestinationProvider,
                request.PlaylistId,
                request.PlaylistName);

            _ = Task.Run(async () =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<MigrationOrchestrator>();
                await orchestrator.RunAsync(job.Id, sessionId);
            });

            return Results.Accepted($"/migrate/{job.Id}", new { jobId = job.Id });
        });

        group.MapGet("/{jobId}", (HttpContext ctx, string jobId, IMigrationJobStore jobStore) =>
        {
            var sessionId = ctx.Session.GetString("session_id");
            if (sessionId is null)
                return Results.Unauthorized();

            var job = jobStore.Get(jobId);
            if (job is null)
                return Results.NotFound();

            return Results.Ok(new
            {
                job.Id,
                job.Status,
                job.SourceProvider,
                job.DestinationProvider,
                job.SourcePlaylistName,
                job.TotalTracks,
                job.ProcessedTracks,
                job.DestinationPlaylistId,
                job.ErrorMessage,
                job.CreatedAt,
                job.CompletedAt,
                Results = job.Results.Select(r => new
                {
                    SourceTrack = new { r.SourceTrack.Title, r.SourceTrack.Artist, r.SourceTrack.Album },
                    MatchedTrack = r.MatchedTrack is not null
                        ? new { r.MatchedTrack.Title, r.MatchedTrack.Artist, r.MatchedTrack.Album }
                        : null,
                    r.Status,
                    r.ConfidenceScore,
                    r.FailReason
                })
            });
        });

        group.MapGet("/", (HttpContext ctx, IMigrationJobStore jobStore) =>
        {
            var sessionId = ctx.Session.GetString("session_id");
            if (sessionId is null)
                return Results.Unauthorized();

            var jobs = jobStore.GetBySession(sessionId);
            return Results.Ok(jobs.Select(j => new
            {
                j.Id,
                j.Status,
                j.SourceProvider,
                j.DestinationProvider,
                j.SourcePlaylistName,
                j.TotalTracks,
                j.ProcessedTracks,
                j.CreatedAt
            }));
        });
    }
}
