namespace MusicMigrator.Core.Models;

public enum MigrationStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public enum MatchStatus
{
    Matched,
    PartialMatch,
    NotFound
}
