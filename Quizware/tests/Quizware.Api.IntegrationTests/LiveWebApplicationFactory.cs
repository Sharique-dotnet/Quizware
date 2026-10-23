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
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"quizware-live-{Guid.NewGuid():N}.db");
    private int _failNextPushes;

    public LiveWebApplicationFactory()
    {
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
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>((sp, options) => options
                .UseSqlite($"Data Source={_path};Default Timeout=30")
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
        if (disposing)
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
