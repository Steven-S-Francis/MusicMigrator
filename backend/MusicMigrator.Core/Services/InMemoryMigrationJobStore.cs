using System.Collections.Concurrent;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Services;

public class InMemoryMigrationJobStore : IMigrationJobStore
{
    private readonly ConcurrentDictionary<string, MigrationJob> _jobs = new();
    private readonly ConcurrentDictionary<string, List<string>> _sessionIndex = new();
    private readonly object _indexLock = new();

    public MigrationJob Create(string sessionId, string sourceProvider, string destProvider, string playlistId, string playlistName)
    {
        var job = new MigrationJob
        {
            SourceProvider = sourceProvider,
            DestinationProvider = destProvider,
            SourcePlaylistId = playlistId,
            SourcePlaylistName = playlistName
        };

        _jobs[job.Id] = job;

        lock (_indexLock)
        {
            var jobIds = _sessionIndex.GetOrAdd(sessionId, _ => []);
            jobIds.Add(job.Id);
        }

        return job;
    }

    public MigrationJob? Get(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    public void Update(MigrationJob job) =>
        _jobs[job.Id] = job;

    public IEnumerable<MigrationJob> GetBySession(string sessionId)
    {
        if (!_sessionIndex.TryGetValue(sessionId, out var jobIds))
            return [];

        List<string> snapshot;
        lock (_indexLock)
        {
            snapshot = [.. jobIds];
        }

        return snapshot
            .Select(id => _jobs.TryGetValue(id, out var job) ? job : null)
            .Where(job => job is not null)!;
    }
}
