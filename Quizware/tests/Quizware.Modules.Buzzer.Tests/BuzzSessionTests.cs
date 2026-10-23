using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Api.IntegrationTests;
using Quizware.Application.Authorization;
using Quizware.Modules.Buzzer.Manual;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>P14-03/06: device mappings, arming, agent-pushed presses (raw
/// frame kept), ranking, collection, BuzzResultsReady — and that none of it
/// touches scores.</summary>
public class BuzzSessionTests : IClassFixture<BuzzerWebApplicationFactory>
{
    private readonly BuzzerWebApplicationFactory _factory;

    public BuzzSessionTests(BuzzerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Armed(MatchTestHarness H, MatchTestHarness.StartedMatch Started, Guid MatchQuestionId, Guid SessionId, IReadOnlyList<Guid> Participants);

    private async Task<(MatchTestHarness H, MatchTestHarness.StartedMatch Started, Guid MatchQuestionId, IReadOnlyList<Guid> Participants)> LiveQuestionAsync()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var question = await h.ServeAsync(started);
        var participants = (await h.StateAsync(started)).Participants.OrderBy(p => p.SeatNumber).Select(p => p.ParticipantId).ToList();
        return (h, started, question.MatchQuestionId, participants);
    }

    private async Task<Armed> ArmedSessionAsync(bool mapDevices = true)
    {
        var (h, started, questionId, participants) = await LiveQuestionAsync();
        if (mapDevices)
        {
            (await h.Client.PutAsJsonAsync($"/api/v1/buzzer/matches/{started.Match.Id}/mappings", new SetDeviceMappingsRequest(
                [new DeviceMappingEntry(1, participants[0]), new DeviceMappingEntry(2, participants[1]), new DeviceMappingEntry(3, participants[2])])))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var arm = await h.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{started.Match.Id}/sessions", new ArmBuzzSessionRequest(started.Segments[0].Id, questionId));
        arm.StatusCode.Should().Be(HttpStatusCode.OK, await arm.Content.ReadAsStringAsync());
        var session = (await arm.Content.ReadFromJsonAsync<ArmBuzzSessionResponse>())!;
        return new Armed(h, started, questionId, session.SessionId, participants);
    }

    private Task<HttpResponseMessage> PressAsync(Guid sessionId, int device, long elapsedMs, string frame = "02010100", string? key = BuzzerWebApplicationFactory.AgentKey) =>
        _factory.CreateAgentClient(key).PostAsJsonAsync($"/api/v1/buzzer/sessions/{sessionId}/presses", new SubmitBuzzPressRequest(device, elapsedMs, frame, "A"));

    [Fact]
    public async Task Mappings_LinkDevicesToParticipants_AndAreReplacedInPlace()
    {
        var (h, started, _, p) = await LiveQuestionAsync();
        var url = $"/api/v1/buzzer/matches/{started.Match.Id}/mappings";

        var first = await h.Client.PutAsJsonAsync(url, new SetDeviceMappingsRequest([new(1, p[0]), new(2, p[1]), new(3, p[2])]));
        var second = await h.Client.PutAsJsonAsync(url, new SetDeviceMappingsRequest([new(1, p[2]), new(2, p[1])]));
        var duplicateDevice = await h.Client.PutAsJsonAsync(url, new SetDeviceMappingsRequest([new(1, p[0]), new(1, p[1])]));
        var notInMatch = await h.Client.PutAsJsonAsync(url, new SetDeviceMappingsRequest([new(1, Guid.NewGuid())]));
        var asOperator = await (await h.CreateClientAsync(Roles.Operator)).PutAsJsonAsync(url, new SetDeviceMappingsRequest([]));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        duplicateDevice.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        notInMatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        asOperator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var rows = await h.ReadDbAsync(db => db.Set<BuzzDeviceMapping>().Where(m => m.MatchId == started.Match.Id).ToListAsync());
        rows.Where(r => !r.IsDeleted).Select(r => (r.DeviceId, r.MatchParticipantId)).Should().BeEquivalentTo(new[] { (1, (Guid?)p[2]), (2, (Guid?)p[1]) });
        rows.Should().ContainSingle(r => r.IsDeleted && r.DeviceId == 3);
        var devices = await h.ReadDbAsync(db => db.MatchParticipants.Where(x => x.MatchId == started.Match.Id).ToDictionaryAsync(x => x.Id, x => x.BuzzDeviceId));
        devices.Should().BeEquivalentTo(new Dictionary<Guid, int?> { [p[0]] = null, [p[1]] = 2, [p[2]] = 1 });
    }

    [Fact]
    public async Task PushedPresses_AreRankedByElapsedTime_MappedToTeams_AndKeptWithTheirRawFrame()
    {
        var a = await ArmedSessionAsync();
        var scoreEventsBefore = await a.H.ReadDbAsync(db => db.ScoreEvents.CountAsync(e => e.MatchId == a.Started.Match.Id));

        (await PressAsync(a.SessionId, 3, 640, "02030100000280fd")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PressAsync(a.SessionId, 1, 215)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PressAsync(a.SessionId, 1, 999)).StatusCode.Should().Be(HttpStatusCode.OK, "a repeat from the same device is ignored, not an error");
        var collect = await a.H.Client.PostAsync($"/api/v1/buzzer/sessions/{a.SessionId}/collect", null);
        var session = (await collect.Content.ReadFromJsonAsync<BuzzSessionResponse>())!;
        var late = await PressAsync(a.SessionId, 2, 100);

        collect.StatusCode.Should().Be(HttpStatusCode.OK);
        session.State.Should().Be("Collected");
        session.RankedPresses.Select(x => (x.DeviceId, x.Rank, x.ElapsedMs, x.MatchParticipantId))
            .Should().Equal((1, 1, 215L, a.Participants[0]), (3, 2, 640L, a.Participants[2]));
        session.RankedPresses[1].RawFrameHex.Should().Be("02030100000280FD");
        session.RankedPresses[0].TeamName.Should().NotBeNullOrEmpty();
        late.StatusCode.Should().Be(HttpStatusCode.Conflict, "presses are accepted only while the session is armed");
        _factory.Provider.Armed.Should().Contain(r => r.SessionId == a.SessionId && r.DeviceIds.SequenceEqual(new[] { 1, 2, 3 }));

        var announced = await a.H.ReadDbAsync(db => db.OutboxMessages
            .Where(m => m.Type == "BuzzResultsReady" && m.PayloadJson.Contains(a.SessionId.ToString())).CountAsync());
        announced.Should().Be(3, "once per accepted press and once on collect");
        (await a.H.ReadDbAsync(db => db.ScoreEvents.CountAsync(e => e.MatchId == a.Started.Match.Id)))
            .Should().Be(scoreEventsBefore, "buzz data never writes scores");
    }

    [Fact]
    public async Task ThePressEndpoint_TakesOnlyTheAgentKey()
    {
        var a = await ArmedSessionAsync(mapDevices: false);

        var noKey = await PressAsync(a.SessionId, 1, 100, key: null);
        var wrongKey = await PressAsync(a.SessionId, 1, 100, key: "not-the-key");
        var asOperator = await a.H.Client.PostAsJsonAsync($"/api/v1/buzzer/sessions/{a.SessionId}/presses", new SubmitBuzzPressRequest(1, 100, "00"));
        var badFrame = await PressAsync(a.SessionId, 1, 100, frame: "XYZ");
        var unknownSession = await PressAsync(Guid.NewGuid(), 1, 100);
        var ok = await PressAsync(a.SessionId, 1, 100);

        noKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asOperator.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a user token is not an agent credential");
        badFrame.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknownSession.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await ok.Content.ReadFromJsonAsync<BuzzSessionResponse>())!;
        session.RankedPresses.Single().MatchParticipantId.Should().BeNull("device 1 has no mapping in this match");
    }

    [Fact]
    public async Task Arming_NeedsTheLiveQuestion_AndShowsInTheLiveState()
    {
        var a = await ArmedSessionAsync();
        var state = await a.H.StateAsync(a.Started);
        var other = await MatchTestHarness.CreateAsync(_factory);

        var rearm = await a.H.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{a.Started.Match.Id}/sessions", new ArmBuzzSessionRequest(a.Started.Segments[0].Id, Guid.NewGuid()));
        var otherProgram = await other.Client.GetAsync($"/api/v1/buzzer/sessions/{a.SessionId}");
        var display = await (await a.H.CreateClientAsync(Roles.Display)).GetAsync($"/api/v1/buzzer/sessions/{a.SessionId}");

        state.Buzzer.Available.Should().BeTrue();
        state.Buzzer.SessionId.Should().Be(a.SessionId);
        state.Buzzer.State.Should().Be("Armed");
        rearm.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherProgram.StatusCode.Should().Be(HttpStatusCode.NotFound);
        display.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_ClosesTheSession_AndANewArmSupersedesAnOldOne()
    {
        var a = await ArmedSessionAsync();

        var second = await a.H.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{a.Started.Match.Id}/sessions", new ArmBuzzSessionRequest(a.Started.Segments[0].Id, a.MatchQuestionId));
        var newId = (await second.Content.ReadFromJsonAsync<ArmBuzzSessionResponse>())!.SessionId;
        var reset = await a.H.Client.PostAsJsonAsync($"/api/v1/buzzer/sessions/{newId}/reset", new ResetBuzzSessionRequest("False start"));
        var old = await a.H.Client.GetFromJsonAsync<BuzzSessionResponse>($"/api/v1/buzzer/sessions/{a.SessionId}");
        var current = await a.H.Client.GetFromJsonAsync<BuzzSessionResponse>($"/api/v1/buzzer/sessions/{newId}");

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        old!.State.Should().Be("Reset");
        old.FailureReason.Should().Contain("Superseded");
        current!.State.Should().Be("Reset");
        current.FailureReason.Should().Be("Reset: False start");
    }
}
