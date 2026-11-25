using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quizware.Application.Abstractions;
using Quizware.Domain.Tournament;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Infrastructure.Backup;

/// <summary>The "Backup" configuration section.</summary>
public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>NFR-A5: back the database up as each match starts.</summary>
    public bool BeforeMatchStart { get; set; } = true;

    /// <summary>A folder on the SQL Server host. Empty means SQL Server's own
    /// default backup folder.</summary>
    public string? Directory { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>NFR-A5: a copy-only, checksummed full backup of the database,
/// taken just before a match's start is saved, so a restore lands on the
/// match exactly as it stood ready to play. Copy-only, so it never disturbs
/// the venue's own backup chain. Both names go in as parameters.</summary>
public sealed class SqlServerDatabaseBackup : IDatabaseBackup
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly BackupOptions _options;
    private readonly ILogger<SqlServerDatabaseBackup> _logger;

    public SqlServerDatabaseBackup(AppDbContext db, IClock clock, IOptions<BackupOptions> options, ILogger<SqlServerDatabaseBackup> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DatabaseBackupResult?> BeforeMatchStartAsync(Match match, CancellationToken cancellationToken)
    {
        if (!_options.BeforeMatchStart || !_db.Database.IsSqlServer())
        {
            return null;
        }

        var database = _db.Database.GetDbConnection().Database;
        var file = FileFor(database, match);
        var previousTimeout = _db.Database.GetCommandTimeout();
        try
        {
            _db.Database.SetCommandTimeout(_options.TimeoutSeconds);
            await _db.Database.ExecuteSqlRawAsync(
                "BACKUP DATABASE @database TO DISK = @file WITH COPY_ONLY, CHECKSUM, INIT",
                [new SqlParameter("@database", database), new SqlParameter("@file", file)],
                cancellationToken);
            _logger.LogInformation("Pre-match backup of {Database} for match {MatchId} written to {File}", database, match.Id, file);
            return new DatabaseBackupResult(true, file, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Pre-match backup of {Database} for match {MatchId} failed; the match starts anyway", database, match.Id);
            return new DatabaseBackupResult(false, file, ex.Message);
        }
        finally
        {
            _db.Database.SetCommandTimeout(previousTimeout);
        }
    }

    private string FileFor(string database, Match match)
    {
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"{database}-prematch-{match.MatchNumber}-{match.Id.ToString("N")[..8]}-{_clock.UtcNow:yyyyMMddHHmmss}.bak");
        if (string.IsNullOrWhiteSpace(_options.Directory))
        {
            return name;
        }

        var directory = _options.Directory;
        var separator = directory.Contains('\\') ? '\\' : '/';
        return directory.TrimEnd('/', '\\') + separator + name;
    }
}
