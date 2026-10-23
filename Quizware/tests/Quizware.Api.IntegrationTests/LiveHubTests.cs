using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>P12-01/02/04/06: the match and display hubs — joining, full-state
/// re-sync, answer stripping for displays, and that neither hub writes.</summary>
public class LiveHubTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveHubTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task JoinMatch_ReturnsTheFullState_AndRequestFullStateResyncsAfterwards()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await using var hub = await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, h.Client);

        var joined = await hub.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        await h.ServeAsync(started);
        var resynced = await hub.InvokeAsync<LiveMatchStateResponse>("RequestFullState");

        joined.MatchId.Should().Be(started.Match.Id);
        joined.CurrentQuestion.Should().BeNull();
        resynced.CurrentQuestion.Should().NotBeNull("the re-sync reads the current state, not a cached one");
    }

    [Fact]
    public async Task TheDisplayHub_WithholdsTheAnswerUntilReveal_WhileTheMatchHubShowsIt()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var displayClient = await h.CreateClientAsync(Roles.Display);
        await using var console = await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, h.Client);
        await using var display = await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, displayClient);
        await using var adminOnDisplay = await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, h.Client);

        var consoleState = await console.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        var displayState = await display.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        var adminDisplayState = await adminOnDisplay.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        (await h.Client.PostAsync($"{started.Live}/questions/{question.MatchQuestionId}/reveal", null)).EnsureSuccessStatusCode();
        var revealed = await display.InvokeAsync<LiveMatchStateResponse>("RequestFullState");

        consoleState.CurrentQuestion!.CorrectOptionId.Should().NotBeNull();
        displayState.CurrentQuestion!.CorrectOptionId.Should().BeNull();
        adminDisplayState.CurrentQuestion!.CorrectOptionId.Should().BeNull("the display hub serves the display view whoever connects");
        revealed.CurrentQuestion!.CorrectOptionId.Should().Be(consoleState.CurrentQuestion.CorrectOptionId);
    }

    [Fact]
    public async Task RequestFullState_BeforeJoining_AndJoiningAnotherProgramsMatch_AreRefused()
    {
        var mine = await MatchTestHarness.CreateAsync(_factory);
        var theirs = await MatchTestHarness.CreateAsync(_factory);
        var theirMatch = await theirs.CreateReadyToStartMatchAsync(2, 2);
        await using var hub = await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, mine.Client);

        var resync = () => hub.InvokeAsync<LiveMatchStateResponse>("RequestFullState");
        var join = () => hub.InvokeAsync<LiveMatchStateResponse>("JoinMatch", theirMatch.Id);

        (await resync.Should().ThrowAsync<HubException>()).Which.Message.Should().Contain("Join a match first");
        (await join.Should().ThrowAsync<HubException>()).Which.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ADisplayToken_CannotConnectToTheMatchHub_AndNobodyConnectsUnauthenticated()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var displayClient = await h.CreateClientAsync(Roles.Display);

        var displayOnMatchHub = () => LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, displayClient);
        var anonymous = LiveHubConnections.Build(_factory, DisplayHub.Path, token: null);

        await displayOnMatchHub.Should().ThrowAsync<Exception>();
        await anonymous.Invoking(c => c.StartAsync()).Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void NeitherHub_ExposesAnythingThatWrites()
    {
        static IEnumerable<string> Methods(Type hub) => hub
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.DeclaringType is { } t && typeof(LiveHubBase).IsAssignableFrom(t))
            .Select(m => m.Name)
            .Distinct();

        Methods(typeof(DisplayHub)).Should().BeEquivalentTo("JoinMatch", "LeaveMatch", "RequestFullState");
        Methods(typeof(MatchHub)).Should().BeEquivalentTo("JoinMatch", "LeaveMatch", "RequestFullState");
    }
}
