using Microsoft.Extensions.Logging;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Services;

public class MigrationOrchestrator(
    IEnumerable<IMusicProvider> providers,
    ITrackMatcher trackMatcher,
    IMigrationJobStore jobStore,
    ITokenStore tokenStore,
    ILogger<MigrationOrchestrator> logger)
{
    private readonly Dictionary<string, IMusicProvider> _providers =
        providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);

    public async Task RunAsync(string jobId, string sessionId, CancellationToken ct = default)
    {
        var job = jobStore.Get(jobId)
                  ?? throw new InvalidOperationException($"Job '{jobId}' not found.");

        try
        {
            // 1. Set status to Running
            job.Status = MigrationStatus.Running;
            jobStore.Update(job);

            // 2. Load source access token
            var sourceToken = tokenStore.Get(sessionId, job.SourceProvider)
                              ?? throw new InvalidOperationException(
                                  $"No token found for source provider '{job.SourceProvider}'.");

            // 3. Load destination access token
            var destToken = tokenStore.Get(sessionId, job.DestinationProvider)
                            ?? throw new InvalidOperationException(
                                $"No token found for destination provider '{job.DestinationProvider}'.");

            // 4. Resolve providers
            if (!_providers.TryGetValue(job.SourceProvider, out var sourceProvider))
                throw new InvalidOperationException($"Unknown source provider '{job.SourceProvider}'.");

            if (!_providers.TryGetValue(job.DestinationProvider, out var destProvider))
                throw new InvalidOperationException($"Unknown destination provider '{job.DestinationProvider}'.");

            // 5. Fetch all source tracks
            var sourceTracks = (await sourceProvider.GetTracksAsync(
                sourceToken.AccessToken, job.SourcePlaylistId, ct)).ToList();
            job.TotalTracks = sourceTracks.Count;
            jobStore.Update(job);

            logger.LogInformation(
                "Job {JobId}: Fetched {Count} tracks from {Provider}.",
                jobId, sourceTracks.Count, job.SourceProvider);

            // 6. Create destination playlist
            var destPlaylistId = await destProvider.CreatePlaylistAsync(
                destToken.AccessToken, job.SourcePlaylistName, null, ct);
            job.DestinationPlaylistId = destPlaylistId;
            jobStore.Update(job);

            logger.LogInformation(
                "Job {JobId}: Created destination playlist '{PlaylistId}' on {Provider}.",
                jobId, destPlaylistId, job.DestinationProvider);

            // 7. Match each source track
            var matchedTracks = new List<Track>();

            foreach (var sourceTrack in sourceTracks)
            {
                ct.ThrowIfCancellationRequested();

                var (match, score, status) = await trackMatcher.FindBestMatchAsync(
                    destProvider, destToken.AccessToken, sourceTrack, ct);

                var result = new TrackMigrationResult(
                    SourceTrack: sourceTrack,
                    MatchedTrack: match,
                    Status: status,
                    ConfidenceScore: score,
                    FailReason: status == MatchStatus.NotFound ? "No suitable match found." : null);

                job.Results.Add(result);
                jobStore.Update(job);

                if (match is not null)
                    matchedTracks.Add(match);

                // Rate-limit delay
                await Task.Delay(150, ct);
            }

            // 8. Add all matched tracks to destination playlist
            if (matchedTracks.Count > 0)
            {
                await destProvider.AddTracksAsync(
                    destToken.AccessToken, destPlaylistId, matchedTracks, ct);

                logger.LogInformation(
                    "Job {JobId}: Added {Count} matched tracks to destination playlist.",
                    jobId, matchedTracks.Count);
            }

            // 9. Mark completed
            job.Status = MigrationStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            jobStore.Update(job);

            logger.LogInformation("Job {JobId}: Migration completed successfully.", jobId);
        }
        catch (OperationCanceledException)
        {
            // 11. Cancellation
            job.Status = MigrationStatus.Failed;
            job.ErrorMessage = "Migration cancelled.";
            jobStore.Update(job);

            logger.LogWarning("Job {JobId}: Migration was cancelled.", jobId);
            throw;
        }
        catch (Exception ex)
        {
            // 10. Unhandled exception
            job.Status = MigrationStatus.Failed;
            job.ErrorMessage = ex.Message;
            jobStore.Update(job);

            logger.LogError(ex, "Job {JobId}: Migration failed.", jobId);
            // Do not rethrow — this runs in a background task
        }
    }
}
