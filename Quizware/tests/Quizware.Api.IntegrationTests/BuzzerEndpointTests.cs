using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>P14-01/02/07: with no buzzer provider configured (the default) —
/// or with the buzzer module deleted — capability and health answer, anything
/// that needs hardware is a 503 BUZZER_UNAVAILABLE, and gameplay is untouched.
/// These tests hold either way, so they also run in the build without the module.</summary>
public class BuzzerEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BuzzerEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CapabilityAndHealth_ReportNoBuzzer_SoTheConsoleShowsManualButtons()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var display = await h.CreateClientAsync(Roles.Display);

        var capability = await display.GetFromJsonAsync<BuzzerCapabilityResponse>("/api/v1/buzzer/capability");
        var health = await h.Client.GetFromJsonAsync<BuzzerHealthResponse>("/api/v1/buzzer/health");
        var devices = await h.Client.GetFromJsonAsync<BuzzerDevicesResponse>("/api/v1/buzzer/devices");

        capability!.Available.Should().BeFalse();
        capability.Provider.Should().Be("None");
        health!.Healthy.Should().BeFalse();
        health.Detail.Should().NotBeNullOrEmpty();
        devices!.Devices.Should().BeEmpty();
    }

    [Fact]
    public async Task EverythingThatNeedsHardware_Returns503BuzzerUnavailable_NeverA500()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);

        var test = await h.Client.PostAsync("/api/v1/buzzer/test", null);
        var arm = await h.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{started.Match.Id}/sessions", new ArmBuzzSessionRequest(started.Segments[0].Id, question.MatchQuestionId));

        foreach (var response in new[] { test, arm })
        {
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, await response.Content.ReadAsStringAsync());
            (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("BUZZER_UNAVAILABLE");
        }
    }

    [Fact]
    public async Task WithoutABuzzer_TheLiveStateSaysSo_AndAnswersAreRecordedAsUsual()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!;

        var answer = await h.AnswerAsync(started, question.MatchQuestionId, holder.ParticipantId, "Correct");
        var state = await h.StateAsync(started);

        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        state.Buzzer.Available.Should().BeFalse();
        state.Buzzer.State.Should().Be("Unavailable");
        state.Participants.Single(p => p.ParticipantId == holder.ParticipantId).Score.Should().Be(10);
    }
}
