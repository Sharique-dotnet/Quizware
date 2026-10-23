using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Api.IntegrationTests;
using Quizware.Application.Abstractions;
using Quizware.Application.Buzzer;
using Quizware.Modules.Buzzer.Abstractions;
using Quizware.Modules.Buzzer.Agent;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>P14-05/06: the agent owns the port and only ever calls out —
/// heartbeat, follow the armed session, push presses — and the API's
/// HttpAgent provider is available exactly while heartbeats arrive.</summary>
public class HttpAgentTests : IClassFixture<HttpAgentWebApplicationFactory>
{
    private readonly HttpAgentWebApplicationFactory _factory;

    public HttpAgentTests(HttpAgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TheAgent_FollowsTheArmedSession_PushesPresses_AndResetsWhenItEnds()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var question = await h.ServeAsync(started);
        var (agent, port) = _factory.CreateAgent();
        foreach (var device in new[] { 1, 2, 3 })
        {
            port.Devices[device] = new int[5];
        }

        await agent.TickAsync(CancellationToken.None);
        var capability = await h.Client.GetFromJsonAsync<BuzzerCapabilityResponse>("/api/v1/buzzer/capability");
        var arm = await h.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{started.Match.Id}/sessions", new ArmBuzzSessionRequest(started.Segments[0].Id, question.MatchQuestionId));
        var sessionId = (await arm.Content.ReadFromJsonAsync<ArmBuzzSessionResponse>())!.SessionId;

        await agent.TickAsync(CancellationToken.None);
        var followed = agent.CurrentSession?.SessionId;
        var armCommands = port.Sent.Count(s => s.Command == 0x53);
        port.Devices[2][1] = 333;
        port.Devices[3][4] = 120;
        await agent.TickAsync(CancellationToken.None);
        await agent.TickAsync(CancellationToken.None);
        var session = await h.Client.GetFromJsonAsync<BuzzSessionResponse>($"/api/v1/buzzer/sessions/{sessionId}");
        var collect = await h.Client.PostAsync($"/api/v1/buzzer/sessions/{sessionId}/collect", null);
        port.Sent.Clear();
        await agent.TickAsync(CancellationToken.None);
        var test = await h.Client.PostAsync("/api/v1/buzzer/test", null);

        capability!.Should().Be(new BuzzerCapabilityResponse(true, "HttpAgent", 3));
        arm.StatusCode.Should().Be(HttpStatusCode.OK);
        followed.Should().Be(sessionId);
        armCommands.Should().Be(3, "each device is armed once when the session appears");
        session!.RankedPresses.Select(p => (p.DeviceId, p.ButtonKey, p.ElapsedMs, p.Rank)).Should().Equal((3, "E", 120L, 1), (2, "B", 333L, 2));
        session.RankedPresses.Should().OnlyContain(p => p.RawFrameHex!.Length == 48);
        collect.StatusCode.Should().Be(HttpStatusCode.OK);
        agent.CurrentSession.Should().BeNull("the collected session is no longer armed");
        port.Sent.Should().Contain(s => s.Command == 0x52, "the agent resets the devices when the session ends");
        test.StatusCode.Should().Be(HttpStatusCode.OK);
        (await test.Content.ReadFromJsonAsync<BuzzerTestResponse>())!.Devices.Should().HaveCount(3);
    }

    [Fact]
    public async Task TheAgentEndpoints_TakeOnlyTheAgentKey()
    {
        var (badAgent, _) = _factory.CreateAgent("wrong-key");
        var anonymous = _factory.CreateClient();

        var tick = () => badAgent.TickAsync(CancellationToken.None);
        var heartbeat = await anonymous.PostAsJsonAsync("/api/v1/buzzer/agent/heartbeat", new AgentHeartbeatRequest("x", null, []));

        (await tick.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        heartbeat.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheAgent_CountsAsGone_OnceItsHeartbeatsStop()
    {
        var clock = new SettableClock { UtcNow = new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc) };
        var options = Options.Create(new BuzzerOptions { Agent = new AgentOptions { StaleAfterSeconds = 10 } });
        var registry = new AgentHeartbeatRegistry(clock, options);
        var adapter = new HttpAgentBuzzerAdapter(registry, options);

        var before = adapter.IsAvailable;
        registry.RecordHeartbeat(new AgentHeartbeat("pc", "COM3", [new AgentDeviceReport(1, true, null, null)]));
        var fresh = adapter.IsAvailable;
        clock.UtcNow = clock.UtcNow.AddSeconds(11);
        var stale = adapter.IsAvailable;
        var collect = () => adapter.CollectAsync(Guid.NewGuid(), [1], CancellationToken.None);

        before.Should().BeFalse();
        fresh.Should().BeTrue();
        stale.Should().BeFalse();
        await collect.Should().ThrowAsync<Quizware.Domain.Common.Exceptions.BuzzerUnavailableException>();
    }

    private sealed class SettableClock : IClock
    {
        public DateTime UtcNow { get; set; }
    }
}
