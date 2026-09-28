using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Domain.Enums;
using Quizware.Domain.Scoring;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 10a: every score event moves the match and stage totals in
/// the same transaction, and the totals always equal the ledger.</summary>
public class ScoringEngineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ScoringEngineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<(Guid Participant, Guid Team)> HolderAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started)
    {
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        var team = (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == holder))).TeamId;
        return (holder, team);
    }

    private static Task<TeamStageScore> StageScoreAsync(MatchTestHarness h, Guid stageId, Guid teamId) =>
        h.ReadDbAsync(db => db.TeamStageScores.SingleAsync(s => s.StageId == stageId && s.TeamId == teamId));

    [Fact]
    public async Task Start_OpensAStageScoreRowPerTeam()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 2));

        var rows = await h.ReadDbAsync(db => db.TeamStageScores.Where(s => s.StageId == started.Match.StageId).ToListAsync());

        rows.Should().HaveCount(3).And.OnlyContain(s => s.TotalPoints == 0 && s.MatchesPlayed == 0);
    }

    [Fact]
    public async Task AnAnswer_MovesTheStageTotal_InTheSameTransaction()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var (holder, team) = await HolderAsync(h, started);

        await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");

        (await StageScoreAsync(h, started.Match.StageId, team)).TotalPoints.Should().Be(10);
    }

    [Fact]
    public async Task Totals_AlwaysEqualTheLedger_ThroughAnswersAndAnUndo()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.AddScoringRuleAsync("Mcq", "Incorrect", null, -3);
        await h.SeedMcqAsync(3);
        var stage = await h.CreateStageAsync(("Mcq", 3));
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2)));
        var first = await h.ServeAsync(started);
        var (holder, team) = await HolderAsync(h, started);
        var answered = (await (await h.AnswerAsync(started, first.MatchQuestionId, holder, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
        await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Wrong button"));
        await h.AnswerAsync(started, first.MatchQuestionId, holder, "Incorrect");

        var matchScore = await h.ReadDbAsync(db => db.TeamMatchScores.SingleAsync(s => s.MatchParticipantId == holder));
        var ledger = await h.ReadDbAsync(db => db.ScoreEvents.Where(e => e.MatchParticipantId == holder).ToListAsync());

        matchScore.TotalPoints.Should().Be(-3);
        ledger.Sum(e => e.Points).Should().Be(matchScore.TotalPoints);
        ledger.Where(e => !e.IsReversed && e.EventType != ScoreEventType.Reversal).Sum(e => e.Points).Should().Be(matchScore.TotalPoints);
        (await StageScoreAsync(h, stage.Id, team)).TotalPoints.Should().Be(-3);
    }

    [Fact]
    public async Task Undo_RestoresTheExactPriorMatchAndStageTotals()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var first = await h.ServeAsync(started);
        var (holder, team) = await HolderAsync(h, started);
        await h.AnswerAsync(started, first.MatchQuestionId, holder, "Correct");
        var second = await h.ServeAsync(started);
        var (secondHolder, _) = await HolderAsync(h, started);
        var before = (await StageScoreAsync(h, started.Match.StageId, team)).TotalPoints;
        var answered = (await (await h.AnswerAsync(started, second.MatchQuestionId, secondHolder, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>())!;

        await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answered.AnswerRecordId}/reverse", new ReverseAnswerRequest("Undo"));

        (await StageScoreAsync(h, started.Match.StageId, team)).TotalPoints.Should().Be(before);
        var secondTeam = (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == secondHolder))).TeamId;
        (await StageScoreAsync(h, started.Match.StageId, secondTeam)).TotalPoints.Should().Be(0);
        (await h.ReadDbAsync(db => db.TeamMatchScores.SingleAsync(s => s.MatchParticipantId == secondHolder)))
            .Should().Match<TeamMatchScore>(s => s.TotalPoints == 0 && s.CorrectCount == 0);
    }

    [Fact]
    public async Task APass_WithAPassRule_MovesTheStageTotalToo()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(2, 2);
        await h.ConfigureTemplatesAsync(match.StageId, allowPassing: true);
        await h.AddScoringRuleAsync("Mcq", "Passed", null, -2);
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var question = await h.ServeAsync(started);
        var (holder, team) = await HolderAsync(h, started);

        await h.Client.PostAsJsonAsync($"{started.Live}/pass", new PassQuestionRequest(question.MatchQuestionId, holder));

        (await StageScoreAsync(h, started.Match.StageId, team)).TotalPoints.Should().Be(-2);
    }

    [Fact]
    public async Task Completion_CountsTheMatchAndTheWin_AndRanksTheStage()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var (holder, team) = await HolderAsync(h, started);
        await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");

        await h.Client.PostAsync($"{started.Live}/end", null);

        var rows = await h.ReadDbAsync(db => db.TeamStageScores.Where(s => s.StageId == started.Match.StageId).ToListAsync());
        rows.Should().OnlyContain(s => s.MatchesPlayed == 1);
        rows.Single(s => s.TeamId == team).Should().Match<TeamStageScore>(s => s.Wins == 1 && s.Rank == 1 && s.TotalPoints == 10);
        rows.Single(s => s.TeamId != team).Should().Match<TeamStageScore>(s => s.Wins == 0 && s.Rank == 2);
    }

    [Fact]
    public async Task StageTotals_AccumulateAcrossMatches()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var first = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 1));
        var q1 = await h.ServeAsync(first);
        var (holder, team) = await HolderAsync(h, first);
        await h.AnswerAsync(first, q1.MatchQuestionId, holder, "Correct");
        await h.Client.PostAsync($"{first.Live}/end", null);
        await h.SeedMcqAsync(1);
        var otherTeam = first.Match.Participants.Single(p => p.TeamId != team).TeamId;
        var second = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(first.Match.StageId, 2, [team, otherTeam]));
        var q2 = await h.ServeAsync(second);
        var (secondHolder, secondTeam) = await HolderAsync(h, second);
        await h.AnswerAsync(second, q2.MatchQuestionId, secondHolder, "Correct");

        var stageScore = await StageScoreAsync(h, first.Match.StageId, secondTeam);
        var expected = secondTeam == team ? 20 : 10;

        stageScore.TotalPoints.Should().Be(expected);
        stageScore.MatchesPlayed.Should().Be(1, "the second match is still in progress");
    }

    [Fact]
    public async Task Abandoning_WithdrawsTheMatchsPointsFromTheStage()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var (holder, team) = await HolderAsync(h, started);
        await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");

        await h.Client.PostAsJsonAsync($"{started.Live}/abandon", new AbandonMatchRequest("Power cut"));

        (await StageScoreAsync(h, started.Match.StageId, team)).TotalPoints.Should().Be(0);
        (await h.ReadDbAsync(db => db.TeamMatchScores.SingleAsync(s => s.MatchParticipantId == holder))).TotalPoints
            .Should().Be(10, "the match's own record is kept");
    }
}
