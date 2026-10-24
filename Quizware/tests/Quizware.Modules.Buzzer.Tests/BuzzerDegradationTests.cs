using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Buzzer;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.IntegrationTests;
using Quizware.Domain.Enums;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>P14-08 and the Phase 14 required tests: the hardware is unplugged
/// mid-match and the match still completes normally on manual entry; a
/// buzzer failure is a 503 that never blocks an answer; and buzz data never
/// writes scores — only the answer the operator records does.</summary>
public class BuzzerDegradationTests : IClassFixture<SerialBuzzerWebApplicationFactory>
{
    private readonly SerialBuzzerWebApplicationFactory _factory;

    public BuzzerDegradationTests(SerialBuzzerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnpluggingTheHardwareMidMatch_FallsBackToManualEntry_AndTheMatchCompletesNormally()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 3));
        var participants = (await h.StateAsync(started)).Participants.OrderBy(p => p.SeatNumber).Select(p => p.ParticipantId).ToList();
        (await h.Client.PutAsJsonAsync($"/api/v1/buzzer/matches/{started.Match.Id}/mappings",
            new SetDeviceMappingsRequest([new(1, participants[0]), new(2, participants[1])]))).EnsureSuccessStatusCode();
        _factory.Port.Devices[1] = new int[5];
        _factory.Port.Devices[2] = new int[5];
        var segment = started.Segments[0].Id;

        // Question 1 — the buzzer works: the team on turn presses first, and the
        // operator records its answer citing the press.
        var q1 = await h.ServeAsync(started);
        var onTurn = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        var device = onTurn == participants[0] ? 1 : 2;
        var session1 = await ArmAsync(h, started, segment, q1.MatchQuestionId);
        _factory.Port.Devices[device][4] = 140;
        var collected = (await (await h.Client.PostAsync($"/api/v1/buzzer/sessions/{session1}/collect", null))
            .Content.ReadFromJsonAsync<BuzzSessionResponse>())!;
        var winner = collected.RankedPresses.First();
        var scoresBeforeAnswer = await ScoreEventCountAsync(h, started);
        var buzzAnswer = await h.Client.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
            q1.MatchQuestionId, winner.MatchParticipantId!.Value, "Correct", null, null, null, 0, "Buzzer", winner.PressId, (int)winner.ElapsedMs));

        // Question 2 — the cable is pulled after arming: collecting fails, the
        // session is marked Failed, and the operator enters the answer by hand.
        var q2 = await h.ServeAsync(started);
        var session2 = await ArmAsync(h, started, segment, q2.MatchQuestionId);
        _factory.Port.Unplugged = true;
        var failedCollect = await h.Client.PostAsync($"/api/v1/buzzer/sessions/{session2}/collect", null);
        var liveAfterFailure = await h.StateAsync(started);
        var manual2 = await h.AnswerAsync(started, q2.MatchQuestionId, liveAfterFailure.ActiveParticipant!.ParticipantId, "Correct");

        // Question 3 — still unplugged: arming is refused up front; manual again.
        var q3 = await h.ServeAsync(started);
        var refusedArm = await h.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{started.Match.Id}/sessions", new ArmBuzzSessionRequest(segment, q3.MatchQuestionId));
        var capability = await h.Client.GetFromJsonAsync<BuzzerCapabilityResponse>("/api/v1/buzzer/capability");
        var manual3 = await h.AnswerAsync(started, q3.MatchQuestionId, (await h.StateAsync(started)).ActiveParticipant!.ParticipantId, "Incorrect");
        var end = await h.Client.PostAsync($"{started.Live}/end", null);
        var final = await h.StateAsync(started);

        buzzAnswer.StatusCode.Should().Be(HttpStatusCode.OK, await buzzAnswer.Content.ReadAsStringAsync());
        failedCollect.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await MatchTestHarness.ErrorCodeAsync(failedCollect)).Should().Be("BUZZER_UNAVAILABLE");
        liveAfterFailure.Buzzer.State.Should().Be("Failed");
        manual2.StatusCode.Should().Be(HttpStatusCode.OK, "a buzzer failure never blocks an answer");
        refusedArm.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        capability!.Available.Should().BeFalse("the console switches to the manual buttons");
        manual3.StatusCode.Should().Be(HttpStatusCode.OK);
        end.StatusCode.Should().Be(HttpStatusCode.OK, await end.Content.ReadAsStringAsync());
        final.State.Should().Be("Completed");
        final.Participants.Sum(p => p.Score).Should().Be(20, "two correct answers, whichever way they were recorded");

        var answers = await h.ReadDbAsync(db => db.AnswerRecords.Where(a => a.MatchId == started.Match.Id).OrderBy(a => a.AnsweredAtUtc).ToListAsync());
        answers.Select(a => a.AnswerSource).Should().Equal(AnswerSource.Buzzer, AnswerSource.Operator, AnswerSource.Operator);
        answers[0].BuzzPressId.Should().Be(winner.PressId);
        answers[0].ResponseTimeMs.Should().Be(140);
        scoresBeforeAnswer.Should().Be(0, "collecting the buzz wrote no score; the recorded answer did");
        _factory.Port.Unplugged = false;
    }

    private static async Task<Guid> ArmAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started, Guid segment, Guid question)
    {
        var arm = await h.Client.PostAsJsonAsync(
            $"/api/v1/buzzer/matches/{started.Match.Id}/sessions", new ArmBuzzSessionRequest(segment, question));
        arm.StatusCode.Should().Be(HttpStatusCode.OK, await arm.Content.ReadAsStringAsync());
        return (await arm.Content.ReadFromJsonAsync<ArmBuzzSessionResponse>())!.SessionId;
    }

    private static Task<int> ScoreEventCountAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started) =>
        h.ReadDbAsync(db => db.ScoreEvents.CountAsync(e => e.MatchId == started.Match.Id));
}
