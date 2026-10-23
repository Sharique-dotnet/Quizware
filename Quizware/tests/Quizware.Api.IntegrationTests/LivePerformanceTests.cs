using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Application.Authorization;
using Xunit.Abstractions;

namespace Quizware.Api.IntegrationTests;

/// <summary>The Phase 12 test: 100 simulated display clients receive an update within 500 ms.</summary>
public class LivePerformanceTests : IClassFixture<LiveWebApplicationFactory>
{
    private const int Displays = 100;

    private readonly LiveWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public LivePerformanceTests(LiveWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task OneHundredDisplays_ReceiveAnAnswer_Within500Milliseconds()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 3));
        var displayClient = await h.CreateClientAsync(Roles.Display);
        var connections = new List<HubConnection>(Displays);
        var served = new List<TaskCompletionSource>(Displays);
        var answered = new List<TaskCompletionSource>(Displays);
        try
        {
            for (var i = 0; i < Displays; i++)
            {
                var connection = await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, displayClient);
                var servedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var answeredSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.On<LiveEventMessage>("QuestionServed", _ => servedSignal.TrySetResult());
                var slot = i;
                connection.On<LiveEventMessage>("AnswerRecorded", _ => answered[slot].TrySetResult());
                await connection.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
                connections.Add(connection);
                served.Add(servedSignal);
                answered.Add(answeredSignal);
            }

            // Warm-up: the first serve and answer through the pipeline pay for
            // JIT and first-use costs on both the request and the push side.
            var warmUp = await h.ServeAsync(started);
            await Task.WhenAll(served.Select(s => s.Task)).WaitAsync(TimeSpan.FromSeconds(10));
            var warmHolder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
            (await h.AnswerAsync(started, warmUp.MatchQuestionId, warmHolder, "Correct")).EnsureSuccessStatusCode();
            await Task.WhenAll(answered.Select(a => a.Task)).WaitAsync(TimeSpan.FromSeconds(10));
            for (var i = 0; i < Displays; i++)
            {
                answered[i] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            var question = await h.ServeAsync(started);
            var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;

            var clock = Stopwatch.StartNew();
            (await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct")).EnsureSuccessStatusCode();
            var requestMs = clock.ElapsedMilliseconds;
            await Task.WhenAll(answered.Select(a => a.Task)).WaitAsync(TimeSpan.FromSeconds(10));
            clock.Stop();

            _output.WriteLine($"{Displays} displays received AnswerRecorded {clock.ElapsedMilliseconds} ms after the request was sent (the request itself took {requestMs} ms).");
            clock.ElapsedMilliseconds.Should().BeLessThan(500);
        }
        finally
        {
            await Task.WhenAll(connections.Select(c => c.DisposeAsync().AsTask()));
        }
    }
}
