using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Application.Authorization;
using Xunit.Abstractions;

namespace Quizware.Api.IntegrationTests;

/// <summary>P15-01, the load test: five operators each play a full match at
/// the same time, and one of those matches is watched by 100 display clients
/// (NFR-SC2). Every gameplay request, every live-state read and every push to
/// the displays is timed against the NFR table:
/// NFR-P1 p95 gameplay &lt; 200 ms, NFR-P2 live state &lt; 150 ms (p95),
/// NFR-P3 push to all displays &lt; 500 ms. The targets are held when the run
/// is against SQL Server (<see cref="LiveWebApplicationFactory.SqlServerVariable"/>).
/// On the default SQLite file database every write waits on one lock, so
/// there the test is a smoke test with a generous ceiling instead.</summary>
[Collection(LoadTestCollection.Name)]
public class LoadTests : IClassFixture<LiveWebApplicationFactory>
{
    private const int Operators = 5;
    private const int Displays = 100;
    private const int QuestionsPerMatch = 15;
    private const int TeamsPerMatch = 3;

    private readonly LiveWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public LoadTests(LiveWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private sealed class Timings
    {
        public ConcurrentBag<double> Gameplay { get; } = [];

        public ConcurrentBag<double> LiveState { get; } = [];

        public ConcurrentBag<double> DisplayPush { get; } = [];
    }

    private static async Task<T> TimeAsync<T>(ConcurrentBag<double>? into, Func<Task<T>> call)
    {
        var clock = Stopwatch.StartNew();
        var result = await call();
        into?.Add(clock.Elapsed.TotalMilliseconds);
        return result;
    }

    private static double Percentile(IEnumerable<double> values, double p)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1)];
    }

    /// <summary>One operator plays one whole match. The first question only
    /// warms the pipeline up and is not measured. When <paramref name="displays"/>
    /// are watching, each answer waits until every display has received it.</summary>
    private static async Task PlayAsync(
        HttpClient op, MatchTestHarness.StartedMatch started, Timings timings, DisplayWatch? displays)
    {
        for (var q = 0; q < QuestionsPerMatch; q++)
        {
            var measured = q > 0 ? timings : null;
            var serve = await TimeAsync(measured?.Gameplay, () => op.PostAsJsonAsync(
                $"{started.Live}/questions/serve", new ServeQuestionRequest(started.Segments[0].Id)));
            serve.EnsureSuccessStatusCode();
            var question = (await serve.Content.ReadFromJsonAsync<CurrentQuestionDto>())!;

            var state = await TimeAsync(measured?.LiveState, () => op.GetFromJsonAsync<LiveMatchStateResponse>($"{started.Live}/state"));

            var arrival = displays?.Expect();
            var sent = Stopwatch.StartNew();
            var answer = await TimeAsync(measured?.Gameplay, () => op.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
                question.MatchQuestionId, state!.ActiveParticipant!.ParticipantId, q % 3 == 0 ? "Incorrect" : "Correct",
                null, null, null, 0, "Operator", null, null)));
            answer.EnsureSuccessStatusCode();
            if (arrival is not null)
            {
                await arrival.WaitAsync(TimeSpan.FromSeconds(15));
                measured?.DisplayPush.Add(sent.Elapsed.TotalMilliseconds);
            }
        }

        (await TimeAsync(timings.Gameplay, () => op.PostAsync($"{started.Live}/end", null))).EnsureSuccessStatusCode();
    }

    /// <summary>Counts AnswerRecorded arrivals across all display connections.</summary>
    private sealed class DisplayWatch
    {
        private readonly int _displays;
        private TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _received;

        public DisplayWatch(int displays)
        {
            _displays = displays;
        }

        public Task Expect()
        {
            Interlocked.Exchange(ref _received, 0);
            _all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _all.Task;
        }

        public void Received()
        {
            if (Interlocked.Increment(ref _received) == _displays)
            {
                _all.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task FiveOperatorsAndOneHundredDisplays_MeetTheResponseTimeTargets()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var matches = new List<MatchTestHarness.StartedMatch>();
        for (var i = 0; i < Operators; i++)
        {
            matches.Add(await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(TeamsPerMatch, QuestionsPerMatch)));
        }

        var operators = new List<HttpClient>();
        for (var i = 0; i < Operators; i++)
        {
            operators.Add(await h.CreateClientAsync(Roles.Operator));
        }

        var displayClient = await h.CreateClientAsync(Roles.Display);
        var watch = new DisplayWatch(Displays);
        var connections = new List<HubConnection>(Displays);
        var timings = new Timings();
        try
        {
            for (var i = 0; i < Displays; i++)
            {
                var connection = await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, displayClient);
                connection.On<LiveEventMessage>("AnswerRecorded", _ => watch.Received());
                await connection.InvokeAsync<LiveMatchStateResponse>("JoinMatch", matches[0].Match.Id);
                connections.Add(connection);
            }

            var clock = Stopwatch.StartNew();
            await Task.WhenAll(matches.Select((m, i) => PlayAsync(operators[i], m, timings, i == 0 ? watch : null)));
            clock.Stop();

            foreach (var m in matches)
            {
                (await h.StateAsync(m)).State.Should().Be("Completed");
            }

            var report = new[]
            {
                ("Gameplay requests (serve, answer, end)", timings.Gameplay, 200d),
                ("Live state reads", timings.LiveState, 150d),
                ($"Answer pushed to all {Displays} displays", timings.DisplayPush, 500d),
            };
            _output.WriteLine($"{Operators} operators × {QuestionsPerMatch} questions, {Displays} displays on match 1, {clock.Elapsed.TotalSeconds:F1} s in total.");
            foreach (var (name, values, target) in report)
            {
                _output.WriteLine(
                    $"{name}: n={values.Count} p50={Percentile(values, 0.50):F1} ms p95={Percentile(values, 0.95):F1} ms " +
                    $"max={values.DefaultIfEmpty().Max():F1} ms (target p95 < {target} ms)");
            }

            var slack = _factory.OnSqlServer ? 1 : 5;
            _output.WriteLine(_factory.OnSqlServer
                ? "Database: SQL Server — NFR targets enforced."
                : $"Database: SQLite — smoke run; targets relaxed {slack}× (set {LiveWebApplicationFactory.SqlServerVariable} for the real check).");
            Percentile(timings.Gameplay, 0.95).Should().BeLessThan(200 * slack, "NFR-P1");
            Percentile(timings.LiveState, 0.95).Should().BeLessThan(150 * slack, "NFR-P2");
            Percentile(timings.DisplayPush, 0.95).Should().BeLessThan(500 * slack, "NFR-P3");
        }
        finally
        {
            await Task.WhenAll(connections.Select(c => c.DisposeAsync().AsTask()));
        }
    }
}

/// <summary>Runs the load test on its own, after the parallel test
/// collections, so other tests never share the machine with the measurement.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LoadTestCollection
{
    public const string Name = "Load test (runs alone)";
}
