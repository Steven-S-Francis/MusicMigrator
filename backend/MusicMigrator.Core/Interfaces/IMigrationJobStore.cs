using MusicMigrator.Core.Models;

namespace MusicMigrator.Core.Interfaces;

public interface IMigrationJobStore
{
    MigrationJob Create(string sessionId, string sourceProvider, string destProvider, string playlistId, string playlistName);

    MigrationJob? Get(string jobId);

    void Update(MigrationJob job);

    IEnumerable<MigrationJob> GetBySession(string sessionId);
}
