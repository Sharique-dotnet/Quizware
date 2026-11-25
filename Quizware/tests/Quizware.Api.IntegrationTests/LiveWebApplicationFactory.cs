using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quizware.Api.Live;
using Quizware.Infrastructure.Persistence;
using Quizware.Infrastructure.Persistence.Interceptors;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 12: a host with the live-push background services running —
/// the outbox dispatcher and the timer ticks poll the database alongside the
/// requests, so this uses a file-based SQLite database (one connection per
/// context, WAL journal) instead of the shared in-memory connection. Retries
/// are fast, and <see cref="FailNextPushes"/> makes the next N pushes throw.</summary>
public sealed class LiveWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Set to a SQL Server connection string (without a database name)
    /// to run these tests against real SQL Server: each factory then creates a
    /// throwaway database and drops it afterwards. The load test only holds the
    /// NFR response-time targets in this mode (P15-01).</summary>
    public const string SqlServerVariable = "QUIZWARE_TEST_SQLSERVER";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"quizware-live-{Guid.NewGuid():N}.db");
    private readonly string? _sqlServer = Environment.GetEnvironmentVariable(SqlServerVariable) is { Length: > 0 } server
        ? $"{server.TrimEnd(';')};Database=QuizwareLive_{Guid.NewGuid():N}"
        : null;

    private int _failNextPushes;

    /// <summary>True when running against SQL Server rather than SQLite.</summary>
    public bool OnSqlServer => _sqlServer is not null;

    public LiveWebApplicationFactory()
    {
        if (_sqlServer is not null)
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var wal = connection.CreateCommand();
        wal.CommandText = "PRAGMA journal_mode=WAL;";
        wal.ExecuteNonQuery();
    }

    public void FailNextPushes(int count) => Interlocked.Exchange(ref _failNextPushes, count);

    internal bool ShouldFailThisPush()
    {
        while (true)
        {
            var current = Volatile.Read(ref _failNextPushes);
            if (current <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _failNextPushes, current - 1, current) == current)
            {
                return true;
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(Quizware.Api.Security.AuthRateLimiting.ConfigurationKey, "100000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>((sp, options) => (_sqlServer is null
                    ? options.UseSqlite($"Data Source={_path};Default Timeout=30")
                    : options.UseSqlServer(_sqlServer))
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .AddInterceptors(
                    sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditLogSaveChangesInterceptor>()));

            services.Configure<LiveOptions>(o =>
            {
                o.BackgroundServicesEnabled = true;
                o.PollIntervalMs = 200;
                o.RetryBaseDelayMs = 20;
                o.MaxRetries = 3;
                o.TimerTickIntervalMs = 200;
            });
            services.AddScoped<SignalRLivePusher>();
            services.AddScoped<ILivePusher>(sp => new FlakyLivePusher(this, sp.GetRequiredService<SignalRLivePusher>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _sqlServer is not null)
        {
            DropSqlServerDatabase();
        }

        if (disposing && _sqlServer is null)
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private void DropSqlServerDatabase()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sqlServer);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        connection.Open();
        using var drop = connection.CreateCommand();
        drop.CommandText = $"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END";
        drop.ExecuteNonQuery();
    }

    private sealed class FlakyLivePusher : ILivePusher
    {
        private readonly LiveWebApplicationFactory _factory;
        private readonly ILivePusher _inner;

        public FlakyLivePusher(LiveWebApplicationFactory factory, ILivePusher inner)
        {
            _factory = factory;
            _inner = inner;
        }

        public Task PushAsync(LiveDispatch dispatch, CancellationToken cancellationToken) =>
            _factory.ShouldFailThisPush()
                ? throw new InvalidOperationException("Simulated SignalR failure.")
                : _inner.PushAsync(dispatch, cancellationToken);
    }
}
