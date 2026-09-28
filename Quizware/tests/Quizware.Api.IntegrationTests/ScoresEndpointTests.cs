using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Scores;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 10b: match scores, the ledger, ProgramAdmin-only manual
/// adjustment, and recalculation from the ledger.</summary>
public class ScoresEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ScoresEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string ScoresUrl(MatchTestHarness.StartedMatch started) => $"/api/v1/matches/{started.Match.Id}/scores";

    private static async Task<(Guid Participant, Guid Team)> ScoreOneCorrectAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started)
    {
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");
        var team = (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == holder))).TeamId;
        return (holder, team);
    }

    [Fact]
    public async Task GetScores_ReturnsEachTeamRanked()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 2));
        var (_, team) = await ScoreOneCorrectAsync(h, started);

        var scores = (await h.Client.GetFromJsonAsync<MatchScoresResponse>(ScoresUrl(started)))!.Scores;

        scores.Should().HaveCount(3);
        scores[0].Should().Match<TeamScoreDto>(s => s.TeamId == team && s.Score == 10 && s.Rank == 1 && s.TeamName.StartsWith("Team "));
        scores.Skip(1).Should().OnlyContain(s => s.Score == 0 && s.Rank == 2);
    }

    [Fact]
    public async Task GetEvents_ListsTheLedger_WithReversalsMarked()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        var answered = (await (await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Mis-click"));

        var events = (await h.Client.GetFromJsonAsync<ScoreEventsResponse>($"{ScoresUrl(started)}/events"))!.Events;

        events.Should().HaveCount(2);
        events[0].Should().Match<ScoreEventDto>(e => e.Points == 10 && e.IsReversed && e.Reason == "Answer: Correct");
        events[1].Should().Match<ScoreEventDto>(e => e.Points == -10 && !e.IsReversed && e.Reason == "Mis-click");
    }

    [Fact]
    public async Task Adjust_AddsAReasonedLedgerEntry_AndMovesMatchAndStageTotals()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var team = started.Match.Participants[0].TeamId;

        var response = await h.Client.PostAsJsonAsync($"{ScoresUrl(started)}/adjust", new AdjustScoreRequest(team, 5, "Bonus for sportsmanship"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<MatchScoresResponse>())!.Scores.Single(s => s.TeamId == team).Score.Should().Be(5);
        var adjustment = await h.ReadDbAsync(db => db.ScoreEvents.SingleAsync(e => e.MatchId == started.Match.Id));
        adjustment.Reason.Should().Be("Bonus for sportsmanship");
        adjustment.ApprovedByUserId.Should().NotBeNull();
        (await h.ReadDbAsync(db => db.TeamStageScores.SingleAsync(s => s.StageId == started.Match.StageId && s.TeamId == team)))
            .TotalPoints.Should().Be(5);
    }

    [Fact]
    public async Task Adjust_NeedsAReasonAndANonZeroChange_AndATeamFromTheMatch()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var team = started.Match.Participants[0].TeamId;

        var noReason = await h.Client.PostAsJsonAsync($"{ScoresUrl(started)}/adjust", new AdjustScoreRequest(team, 5, ""));
        var zero = await h.Client.PostAsJsonAsync($"{ScoresUrl(started)}/adjust", new AdjustScoreRequest(team, 0, "Nothing"));
        var stranger = await h.Client.PostAsJsonAsync($"{ScoresUrl(started)}/adjust", new AdjustScoreRequest(Guid.NewGuid(), 5, "Who?"));

        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        zero.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        stranger.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Roles.Operator)]
    [InlineData(Roles.Scorer)]
    public async Task Adjust_ByOperatorOrScorer_IsForbidden(string role)
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var client = await h.CreateClientAsync(role);

        var adjust = await client.PostAsJsonAsync(
            $"{ScoresUrl(started)}/adjust", new AdjustScoreRequest(started.Match.Participants[0].TeamId, 5, "Nope"));
        var recalculate = await client.PostAsync($"{ScoresUrl(started)}/recalculate", null);

        adjust.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        recalculate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Adjust_OnACompletedMatch_CanChangeTheWinner()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var (_, leader) = await ScoreOneCorrectAsync(h, started);
        await h.Client.PostAsync($"{started.Live}/end", null);
        var trailer = started.Match.Participants.Single(p => p.TeamId != leader).TeamId;

        await h.Client.PostAsJsonAsync($"{ScoresUrl(started)}/adjust", new AdjustScoreRequest(trailer, 15, "Appeal upheld"));

        (await h.ReadDbAsync(db => db.Matches.SingleAsync(m => m.Id == started.Match.Id))).WinnerTeamId.Should().Be(trailer);
        var stage = await h.ReadDbAsync(db => db.TeamStageScores.Where(s => s.StageId == started.Match.StageId).ToListAsync());
        stage.Single(s => s.TeamId == trailer).Should().Match<Domain.Scoring.TeamStageScore>(s => s.Wins == 1 && s.TotalPoints == 15 && s.Rank == 1);
        stage.Single(s => s.TeamId == leader).Wins.Should().Be(0);
    }

    [Fact]
    public async Task Recalculate_FromTheLedger_EqualsTheIncrementalTotals()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 3));
        await ScoreOneCorrectAsync(h, started);
        var second = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        var answered = (await (await h.AnswerAsync(started, second.MatchQuestionId, holder, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Undo"));
        await h.Client.PostAsJsonAsync($"{ScoresUrl(started)}/adjust", new AdjustScoreRequest(started.Match.Participants[1].TeamId, 7, "Bonus"));
        var incremental = await h.ReadDbAsync(db => db.TeamMatchScores.Where(s => s.MatchId == started.Match.Id)
            .Select(s => new { s.TeamId, s.TotalPoints, s.CorrectCount }).ToListAsync());
        var stageBefore = await h.ReadDbAsync(db => db.TeamStageScores.Where(s => s.StageId == started.Match.StageId)
            .Select(s => new { s.TeamId, s.TotalPoints }).ToListAsync());

        var response = await h.Client.PostAsync($"{ScoresUrl(started)}/recalculate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rebuilt = await h.ReadDbAsync(db => db.TeamMatchScores.Where(s => s.MatchId == started.Match.Id)
            .Select(s => new { s.TeamId, s.TotalPoints, s.CorrectCount }).ToListAsync());
        var stageAfter = await h.ReadDbAsync(db => db.TeamStageScores.Where(s => s.StageId == started.Match.StageId)
            .Select(s => new { s.TeamId, s.TotalPoints }).ToListAsync());
        rebuilt.Should().BeEquivalentTo(incremental);
        stageAfter.Should().BeEquivalentTo(stageBefore);
    }

    [Fact]
    public async Task Recalculate_RepairsATotalThatDriftedFromTheLedger()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var (participant, team) = await ScoreOneCorrectAsync(h, started);
        await h.SeedAsync((db, _) =>
        {
            db.TeamMatchScores.Single(s => s.MatchParticipantId == participant).ApplyAdjustment(500);
            db.TeamStageScores.Single(s => s.StageId == started.Match.StageId && s.TeamId == team).ApplyPoints(500);
        });

        var response = await h.Client.PostAsync($"{ScoresUrl(started)}/recalculate", null);

        (await response.Content.ReadFromJsonAsync<RecalculateScoresResponse>())!.Scores.Single(s => s.TeamId == team).Score.Should().Be(10);
        (await h.ReadDbAsync(db => db.TeamStageScores.SingleAsync(s => s.StageId == started.Match.StageId && s.TeamId == team)))
            .TotalPoints.Should().Be(10);
    }

    [Fact]
    public async Task Recalculate_BeforeStart_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(2, 2);

        var response = await h.Client.PostAsync($"/api/v1/matches/{match.Id}/scores/recalculate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
