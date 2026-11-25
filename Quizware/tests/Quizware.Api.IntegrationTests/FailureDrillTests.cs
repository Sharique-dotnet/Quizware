using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Application.Authorization;
using Quizware.Infrastructure.Persistence;
using Quizware.Infrastructure.Persistence.Interceptors;

namespace Quizware.Api.IntegrationTests;

/// <summary>P15-04, the failure drills that can run in-process (the hard
/// process kill and the database outage run against real SQL Server in
/// tools/drills/failure_drills.py): the API restarting mid-match loses
/// nothing and is back well inside 30 s (NFR-A1/A2), a display that drops
/// does not affect the match and gets the whole state back on rejoining
/// (NFR-A4), and a database outage is a retryable 503, not a 500.</summary>
public class FailureDrillTests : IClassFixture<LiveWebApplicationFactory>
{
    private readonly LiveWebApplicationFactory _factory;

    public FailureDrillTests(LiveWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>An API host on a SQLite file that outlives it, so a second
    /// host can pick up where the first stopped.</summary>
    private sealed class FileHost(string path) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(Quizware.Api.Security.AuthRateLimiting.ConfigurationKey, "100000");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
                services.AddDbContext<AppDbContext>((sp, options) => options
                    .UseSqlite($"Data Source={path};Default Timeout=30")
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                    .AddInterceptors(
                        sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                        sp.GetRequiredService<AuditLogSaveChangesInterceptor>()));
                services.Configure<Quizware.Api.Live.LiveOptions>(o => o.PollIntervalMs = 100);
            });
        }
    }

    [Fact]
    public async Task RestartingTheApiMidMatch_LosesNothing_AndTheMatchCarriesOn()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quizware-restart-{Guid.NewGuid():N}.db");
        try
        {
            LiveMatchStateResponse before;
            MatchTestHarness.StartedMatch started;
            AuthenticationHeaderValue token;
            int answersBefore;
            using (var first = new FileHost(path))
            {
                var h = await MatchTestHarness.CreateAsync(first);
                started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 6));
                for (var i = 0; i < 3; i++)
                {
                    var question = await h.ServeAsync(started);
                    var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
                    (await h.AnswerAsync(started, question.MatchQuestionId, holder, i == 1 ? "Incorrect" : "Correct")).EnsureSuccessStatusCode();
                }

                await h.ServeAsync(started);
                before = await h.StateAsync(started);
                answersBefore = await h.ReadDbAsync(db => db.AnswerRecords.CountAsync(a => a.MatchId == started.Match.Id));
                token = h.Client.DefaultRequestHeaders.Authorization!;
            }

            var clock = Stopwatch.StartNew();
            using var second = new FileHost(path);
            var client = second.CreateClient();
            client.DefaultRequestHeaders.Authorization = token;
            var after = (await client.GetFromJsonAsync<LiveMatchStateResponse>($"{started.Live}/state"))!;
            var recovery = clock.Elapsed;

            after.Should().BeEquivalentTo(before, o => o.Excluding(s => s.CurrentQuestion!.ServerNowUtc),
                "the restarted API resumes the exact state: same question live, same scores, same turn");
            recovery.Should().BeLessThan(TimeSpan.FromSeconds(30), "NFR-A2");

            var answer = await client.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
                after.CurrentQuestion!.MatchQuestionId, after.ActiveParticipant!.ParticipantId, "Correct", null, null, null, 0, "Operator", null, null));
            answer.EnsureSuccessStatusCode();

            using var scope = second.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.AnswerRecords.CountAsync(a => a.MatchId == started.Match.Id)).Should().Be(answersBefore + 1, "NFR-A1: nothing lost");
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (await db.OutboxMessages.AnyAsync(m => m.ProcessedAtUtc == null) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            (await db.OutboxMessages.CountAsync(m => m.ProcessedAtUtc == null)).Should().Be(0, "live events queued before the restart are still delivered");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task ADisplayThatDrops_DoesNotAffectTheMatch_AndGetsEverythingBackOnRejoining()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 4));
        var displayClient = await h.CreateClientAsync(Roles.Display);

        var display = await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, displayClient);
        await display.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        await display.DisposeAsync();

        // The match goes on while the screen is gone.
        for (var i = 0; i < 2; i++)
        {
            var question = await h.ServeAsync(started);
            var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
            (await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct")).EnsureSuccessStatusCode();
        }

        await using var rejoined = await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, displayClient);
        var pushed = new TaskCompletionSource<LiveEventMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        rejoined.On<LiveEventMessage>("QuestionServed", m => pushed.TrySetResult(m));
        var state = await rejoined.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        await h.ServeAsync(started);
        var next = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        state.Participants.Sum(p => p.Score).Should().Be(20, "both answers made while the display was away are in its state");
        state.Progress.QuestionsServed.Should().Be(2);
        next.State!.Progress.QuestionsServed.Should().Be(3, "the rejoined display is back on the live feed");
    }

    [Fact]
    public void ADatabaseOutage_IsRecognised_ButAnOrdinaryDatabaseErrorIsNot()
    {
        DatabaseFailures.IsUnavailable(new TimeoutException()).Should().BeTrue();
        DatabaseFailures.IsUnavailable(new InvalidOperationException("wrapped", new TimeoutException())).Should().BeTrue();
        DatabaseFailures.IsUnavailable(new Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException("gave up")).Should().BeTrue();
        DatabaseFailures.IsUnavailable(new DbUpdateException("duplicate key")).Should().BeFalse();
        DatabaseFailures.IsUnavailable(new KeyNotFoundException()).Should().BeFalse();
    }
}
