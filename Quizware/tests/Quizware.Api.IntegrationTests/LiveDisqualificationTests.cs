using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 9e: disqualification and reinstatement — the fix for the
/// legacy QuestionNumber % 3 rotation that kept serving a removed team.</summary>
public class LiveDisqualificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveDisqualificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Disqualify_RemovesTheTeamFromTheRotation_ButKeepsItsScore()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 6));
        var first = await h.ServeAsync(started);
        var removed = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, first.MatchQuestionId, removed, "Correct");

        var response = await h.DisqualifyAsync(started, removed);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<DisqualifyParticipantResponse>())!;
        result.Status.Should().Be("Disqualified");
        result.MatchCanContinue.Should().BeTrue();
        result.TurnOrderRecalculated.Should().BeTrue();
        result.RemainingActiveParticipants.Select(p => p.TurnOrder).Should().Equal(1, 2);
        result.RemainingActiveParticipants.Should().NotContain(p => p.ParticipantId == removed);
        result.MatchCompleted.Should().BeFalse();

        var targets = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var question = await h.ServeAsync(started);
            targets.Add((await h.StateAsync(started)).ActiveParticipant!.ParticipantId);
            await h.Client.PostAsJsonAsync($"{started.Live}/questions/{question.MatchQuestionId}/skip", new SkipQuestionRequest("next"));
        }

        targets.Should().NotContain(removed, "a disqualified team is never served again");
        targets.Distinct().Should().HaveCount(2);
        (await h.StateAsync(started)).Participants.Single(p => p.ParticipantId == removed)
            .Should().Match<LiveParticipantScoreDto>(p => p.Status == "Disqualified" && p.Score == 10);
    }

    [Fact]
    public async Task Disqualify_TheTeamHoldingTheQuestion_SkipsIt_RatherThanAnsweringForThem()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;

        await h.DisqualifyAsync(started, holder);

        (await h.ReadDbAsync(db => db.MatchQuestions.SingleAsync(q => q.Id == question.MatchQuestionId))).State
            .Should().Be(MatchQuestionState.Skipped);
        (await h.ReadDbAsync(db => db.AnswerRecords.CountAsync(a => a.MatchId == started.Match.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Disqualify_LeavingOneTeam_CompletesTheMatch_WithThatTeamAsWinner()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 3));
        await h.ServeAsync(started);
        var state = await h.StateAsync(started);
        var removed = state.ActiveParticipant!.ParticipantId;
        var survivor = state.Participants.Single(p => p.ParticipantId != removed).ParticipantId;

        var result = (await (await h.DisqualifyAsync(started, removed)).Content.ReadFromJsonAsync<DisqualifyParticipantResponse>())!;

        result.MatchCanContinue.Should().BeFalse();
        result.MatchCompleted.Should().BeTrue();
        var survivorTeam = (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == survivor))).TeamId;
        result.WinnerTeamId.Should().Be(survivorTeam);
        (await h.StateAsync(started)).State.Should().Be("Completed");
    }

    [Fact]
    public async Task Disqualify_WhilePaused_LeavingOneTeam_StillCompletes()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await h.Client.PostAsync($"{started.Live}/pause", null);
        var removed = (await h.StateAsync(started)).Participants[0].ParticipantId;

        var result = (await (await h.DisqualifyAsync(started, removed)).Content.ReadFromJsonAsync<DisqualifyParticipantResponse>())!;

        result.MatchCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Disqualify_RebalancePolicy_TrimsTheSegmentToEqualTurns()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 6);
        await h.ConfigureStageAsync(match.StageId, TeamCountChangePolicy.Rebalance);
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var question = await h.ServeAsync(started);
        await h.Client.PostAsJsonAsync($"{started.Live}/questions/{question.MatchQuestionId}/skip", new SkipQuestionRequest("x"));
        var removed = (await h.StateAsync(started)).Participants[2].ParticipantId;

        var result = (await (await h.DisqualifyAsync(started, removed)).Content.ReadFromJsonAsync<DisqualifyParticipantResponse>())!;

        result.CurrentSegmentAdjustment!.Policy.Should().Be("Rebalance");
        result.CurrentSegmentAdjustment.PlannedQuestionCountBefore.Should().Be(6);
        result.CurrentSegmentAdjustment.PlannedQuestionCountAfter.Should().Be(5, "1 served + 4 left, a multiple of the 2 remaining teams");
        (await h.ReadDbAsync(db => db.MatchQuestions.CountAsync(
            q => q.MatchSegmentId == started.Segments[0].Id && q.State == MatchQuestionState.Released))).Should().Be(1);
    }

    [Fact]
    public async Task Disqualify_TruncatePolicy_EndsTheSegmentAfterWhatWasServed()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 4);
        await h.ConfigureStageAsync(match.StageId, TeamCountChangePolicy.Truncate);
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var question = await h.ServeAsync(started);
        await h.Client.PostAsJsonAsync($"{started.Live}/questions/{question.MatchQuestionId}/skip", new SkipQuestionRequest("x"));
        var removed = (await h.StateAsync(started)).Participants[1].ParticipantId;

        var result = (await (await h.DisqualifyAsync(started, removed)).Content.ReadFromJsonAsync<DisqualifyParticipantResponse>())!;
        var serveMore = await h.Client.PostAsJsonAsync($"{started.Live}/questions/serve", new ServeQuestionRequest(started.Segments[0].Id));

        result.CurrentSegmentAdjustment!.PlannedQuestionCountAfter.Should().Be(1);
        serveMore.StatusCode.Should().Be(HttpStatusCode.Conflict, "nothing is left to serve in a truncated segment");
    }

    [Fact]
    public async Task Disqualify_KeepPlannedPolicy_LeavesTheSegmentAlone()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 4));
        var removed = (await h.StateAsync(started)).Participants[1].ParticipantId;

        var result = (await (await h.DisqualifyAsync(started, removed)).Content.ReadFromJsonAsync<DisqualifyParticipantResponse>())!;

        result.CurrentSegmentAdjustment!.Policy.Should().Be("KeepPlanned");
        result.CurrentSegmentAdjustment.PlannedQuestionCountAfter.Should().Be(4);
    }

    [Fact]
    public async Task Disqualify_NeedsProgramAdmin_AndAnActiveTeamDuringPlay()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 3);
        var beforeStart = await h.Client.PostAsJsonAsync(
            $"{MatchTestHarness.LiveUrl(match.Id)}/participants/{match.Participants[0].Id}/disqualify",
            new DisqualifyParticipantRequest("x", Guid.NewGuid(), true));
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var operatorClient = await h.CreateClientAsync(Roles.Operator);
        var removed = match.Participants[0].Id;

        var asOperator = await operatorClient.PostAsJsonAsync(
            $"{started.Live}/participants/{removed}/disqualify", new DisqualifyParticipantRequest("x", Guid.NewGuid(), true));
        var noReason = await h.Client.PostAsJsonAsync(
            $"{started.Live}/participants/{removed}/disqualify", new DisqualifyParticipantRequest("", Guid.NewGuid(), true));
        await h.DisqualifyAsync(started, removed);
        var twice = await h.DisqualifyAsync(started, removed);

        beforeStart.StatusCode.Should().Be(HttpStatusCode.Conflict);
        asOperator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        twice.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reinstate_RejoinsAtTheEndOfTheRotation()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var first = (await h.StateAsync(started)).Participants.Single(p => p.TurnOrder == 1).ParticipantId;
        await h.DisqualifyAsync(started, first);
        var url = $"{started.Live}/participants/{first}/reinstate";

        var noReason = await h.Client.PostAsJsonAsync(url, new ReinstateParticipantRequest(" "));
        var reinstated = await h.Client.PostAsJsonAsync(url, new ReinstateParticipantRequest("Appeal upheld"));
        var again = await h.Client.PostAsJsonAsync(url, new ReinstateParticipantRequest("Twice"));

        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reinstated.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = (await reinstated.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!;
        state.Participants.Single(p => p.ParticipantId == first).Should().Match<LiveParticipantScoreDto>(p => p.Status == "Active" && p.TurnOrder == 3);
        state.Participants.Select(p => p.TurnOrder).Order().Should().Equal(1, 2, 3);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DisqualifiedButStillStanding_IsRankedAtTheEnd()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var question = await h.ServeAsync(started);
        var scorer = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, question.MatchQuestionId, scorer, "Correct");

        await h.DisqualifyAsync(started, scorer, excludeFromStandings: false);
        await h.Client.PostAsync($"{started.Live}/end", null);

        var participant = await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == scorer));
        participant.FinalRank.Should().Be(1);
        participant.FinalScore.Should().Be(10);
    }

    [Fact]
    public async Task LiveReorder_WhenStageAllowsIt_MovesOnlyPendingSegments()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(2, 1, 1, 1);
        await h.ConfigureStageAsync(match.StageId, TeamCountChangePolicy.KeepPlanned, allowReorderDuringMatch: true);
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var ids = started.Segments.Select(s => s.Id).ToList();

        var response = await h.Client.PutAsJsonAsync(
            $"{started.Live}/segments/reorder", new ReorderMatchSegmentsRequest([ids[2], ids[0], ids[1]], "Crowd favourite first"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var segments = (await response.Content.ReadFromJsonAsync<MatchSegmentsResponse>())!.Segments;
        segments.Select(s => s.Id).Should().Equal(ids[0], ids[2], ids[1]);
        var timeline = (await h.Client.GetFromJsonAsync<MatchTimelineResponse>($"{started.Live}/timeline"))!;
        timeline.Events.Should().Contain(e => e.EventType == "SegmentsReordered");
    }
}
