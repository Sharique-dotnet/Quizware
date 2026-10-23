using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>P12-05: server-authoritative timer ticks reach every screen with the same countdown.</summary>
public class LiveTimerTests : IClassFixture<LiveWebApplicationFactory>
{
    private readonly LiveWebApplicationFactory _factory;

    public LiveTimerTests(LiveWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryScreen_ReceivesTheSameServerCountdown_ForATimedQuestion()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(2, 2);
        await h.ConfigureTemplatesAsync(match.StageId, timeLimitSeconds: 30);
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var displayClient = await h.CreateClientAsync(Roles.Display);
        var screens = new List<HubConnection>
        {
            await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, h.Client),
            await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, displayClient),
            await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, displayClient),
        };
        var ticks = screens.Select(_ => new ConcurrentQueue<TimerTickMessage>()).ToList();
        for (var i = 0; i < screens.Count; i++)
        {
            var queue = ticks[i];
            screens[i].On<TimerTickMessage>("TimerTick", t => queue.Enqueue(t));
            await screens[i].InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        }

        var question = await h.ServeAsync(started);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (ticks.Any(q => q.Count < 2) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        var rest = await h.StateAsync(started);
        foreach (var screen in screens)
        {
            await screen.DisposeAsync();
        }

        ticks.Should().OnlyContain(q => q.Count >= 2, "every screen gets a tick every interval");
        var firstTickTimes = ticks.Select(q => q.First().ServerNowUtc).ToList();
        var common = firstTickTimes.Max();
        var sameTick = ticks.Select(q => q.First(t => t.ServerNowUtc == common || t.ServerNowUtc > common)).ToList();
        sameTick.Select(t => t.RemainingSeconds).Distinct().Should().HaveCount(1, "one server computation is sent to every screen");
        sameTick[0].MatchQuestionId.Should().Be(question.MatchQuestionId);
        sameTick[0].TimeLimitSeconds.Should().Be(30);
        sameTick[0].RemainingSeconds.Should().BeInRange(25, 30);
        rest.CurrentQuestion!.RemainingSeconds!.Value.Should().BeApproximately(ticks[0].Last().RemainingSeconds, 1.5, "the REST state and the ticks share one clock");
    }
}
