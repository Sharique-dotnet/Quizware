using System.Collections.Concurrent;
using System.Data.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Quizware.Api.IntegrationTests;

/// <summary>P15-06: counts the database reads each gameplay request makes.
/// Each read is one round trip on SQL Server (writes go in one batch on
/// SaveChanges), so these ceilings stop a change from quietly adding round
/// trips. NFR-P4 asks for at most 3 round trips per answer; recording an
/// answer takes more today — see docs/hardening/Load-Test.md.</summary>
public class QueryBudgetTests : IClassFixture<QueryBudgetTests.CountingFactory>
{
    private readonly CountingFactory _factory;
    private readonly ITestOutputHelper _output;

    public QueryBudgetTests(CountingFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    public sealed class CommandCounter : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commands = new();

        public bool Recording { get; set; }

        public IReadOnlyList<string> Take()
        {
            var list = new List<string>();
            while (_commands.TryDequeue(out var c))
            {
                list.Add(c);
            }

            return list;
        }

        private void Record(DbCommand command)
        {
            if (Recording)
            {
                _commands.Enqueue(command.CommandText);
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Record(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    public sealed class CountingFactory : CustomWebApplicationFactory
    {
        public CommandCounter Counter { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<Quizware.Infrastructure.Persistence.AppDbContext>(options => options.AddInterceptors(Counter)));
        }
    }

    /// <summary>The number of reads (round trips) the call made.</summary>
    private async Task<int> CountAsync(string name, Func<Task<HttpResponseMessage>> call)
    {
        _factory.Counter.Take();
        _factory.Counter.Recording = true;
        (await call()).EnsureSuccessStatusCode();
        _factory.Counter.Recording = false;
        var commands = _factory.Counter.Take();
        var reads = commands.Count(c => c.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"{name}: {reads} reads, {commands.Count} commands in all");
        foreach (var c in commands)
        {
            _output.WriteLine("   " + c.ReplaceLineEndings(" ")[..Math.Min(c.Length, 140)]);
        }

        return reads;
    }

    [Fact]
    public async Task GameplayRequests_StayWithinTheirQueryBudget()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, (await h.StateAsync(started)).CurrentQuestion!.MatchQuestionId, holder, "Correct");

        var serve = await CountAsync("serve", () => h.Client.PostAsync($"{started.Live}/questions/serve",
            System.Net.Http.Json.JsonContent.Create(new Contracts.V1.LiveMatch.ServeQuestionRequest(started.Segments[0].Id))));
        var state = await CountAsync("state", () => h.Client.GetAsync($"{started.Live}/state"));
        var live = await h.StateAsync(started);
        var answer = await CountAsync("answer", () => h.AnswerAsync(
            started, live.CurrentQuestion!.MatchQuestionId, live.ActiveParticipant!.ParticipantId, "Correct"));

        state.Should().BeLessThanOrEqualTo(10, "live state is read for every screen and every push");
        serve.Should().BeLessThanOrEqualTo(11);
        answer.Should().BeLessThanOrEqualTo(17);
    }
}
