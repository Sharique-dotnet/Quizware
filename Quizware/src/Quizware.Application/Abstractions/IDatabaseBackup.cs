using Quizware.Domain.Tournament;

namespace Quizware.Application.Abstractions;

public sealed record DatabaseBackupResult(bool Succeeded, string? File, string? Error);

/// <summary>NFR-A5: a database backup taken automatically before each match
/// starts. Returns null when backups are off or the database cannot take one
/// (e.g. the SQLite test database). A failed backup is reported, never thrown:
/// it must not stop the show.</summary>
public interface IDatabaseBackup
{
    Task<DatabaseBackupResult?> BeforeMatchStartAsync(Match match, CancellationToken cancellationToken);
}

public sealed class NoDatabaseBackup : IDatabaseBackup
{
    public Task<DatabaseBackupResult?> BeforeMatchStartAsync(Match match, CancellationToken cancellationToken) =>
        Task.FromResult<DatabaseBackupResult?>(null);
}
