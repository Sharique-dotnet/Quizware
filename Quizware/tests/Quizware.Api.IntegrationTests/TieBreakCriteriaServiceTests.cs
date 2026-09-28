using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Scores;
using Quizware.Application.Qualification;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 10c: ordering teams level on stage points by the stage's
/// ordered tie-break criteria, and recording which criterion decided.</summary>
public class TieBreakCriteriaServiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TieBreakCriteriaServiceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static Task<TieBreakOrdering> OrderAsync(MatchTestHarness h, Guid stageId, params Guid[] teams) =>
        h.UseServiceAsync<ITieBreakCriteriaService, TieBreakOrdering>(s => s.OrderTiedTeamsAsync(stageId, teams, CancellationToken.None));

    private static async Task<Guid> TeamOfAsync(MatchTestHarness h, Guid participantId) =>
        (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == participantId))).TeamId;

    [Fact]
    public async Task WithoutAStageRule_UsesTheDocumentedDefaultOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 1));

        var criteria = await h.UseServiceAsync<ITieBreakCriteriaService, IReadOnlyList<string>>(
            s => s.CriteriaForStageAsync(stage.Id, CancellationToken.None));

        criteria.Should().Equal("TotalScore", "FewerIncorrect", "MoreCorrectAtHighDifficulty", "FasterAverageBuzzTime", "HeadToHead");
    }

    [Fact]
    public async Task TheProgramsStageQualificationRule_IsUsed_WithItsOldCriterionNameMapped()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 1));
        (await h.Client.PostAsync($"/api/v1/programs/{h.ProgramId}/rules/tie-break/reset-defaults", null)).EnsureSuccessStatusCode();

        var criteria = await h.UseServiceAsync<ITieBreakCriteriaService, IReadOnlyList<string>>(
            s => s.CriteriaForStageAsync(stage.Id, CancellationToken.None));

        criteria.Should().Equal("HeadToHead", "MoreCorrectAtHighDifficulty");
    }

    [Fact]
    public async Task FewerIncorrect_SeparatesTeamsLevelOnPoints()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var first = await h.ServeAsync(started);
        var clean = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, first.MatchQuestionId, clean, "Correct");
        var second = await h.ServeAsync(started);
        var sloppy = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, second.MatchQuestionId, sloppy, "Incorrect");
        var (cleanTeam, sloppyTeam) = (await TeamOfAsync(h, clean), await TeamOfAsync(h, sloppy));
        await h.Client.PostAsJsonAsync($"/api/v1/matches/{started.Match.Id}/scores/adjust", new AdjustScoreRequest(sloppyTeam, 10, "Level them"));

        var ordering = await OrderAsync(h, started.Match.StageId, sloppyTeam, cleanTeam);

        ordering.RankedGroups.Should().BeEquivalentTo(new[] { new[] { cleanTeam }, new[] { sloppyTeam } }, o => o.WithStrictOrdering());
        ordering.DecidingCriterion.Should().Be("FewerIncorrect");
        ordering.IsFullyResolved.Should().BeTrue();
        ordering.Trace.Single(t => t.Criterion == "TotalScore").Separated.Should().BeFalse();
        ordering.Trace.Single(t => t.Criterion == "FewerIncorrect").Values[sloppyTeam].Should().Be(-1);
    }

    [Fact]
    public async Task HeadToHead_DecidesWhenEverythingCheaperIsLevel()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedMcqAsync(2);
        var stage = await h.CreateStageAsync(("Mcq", 1));
        var teams = await h.CreateTeamsAsync(3);
        var meeting = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, [teams[0], teams[1]]));
        var question = await h.ServeAsync(meeting);
        var winnerParticipant = (await h.StateAsync(meeting)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(meeting, question.MatchQuestionId, winnerParticipant, "Correct");
        await h.Client.PostAsync($"{meeting.Live}/end", null);
        var winner = await TeamOfAsync(h, winnerParticipant);
        var loser = winner == teams[0] ? teams[1] : teams[0];
        var other = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 2, [loser, teams[2]]));
        await h.Client.PostAsJsonAsync($"/api/v1/matches/{other.Match.Id}/scores/adjust", new AdjustScoreRequest(loser, 10, "Catch up"));

        var ordering = await OrderAsync(h, stage.Id, loser, winner);

        ordering.RankedGroups[0].Should().Equal(winner);
        ordering.DecidingCriterion.Should().Be("HeadToHead");
        ordering.Trace.Single(t => t.Criterion == "FasterAverageBuzzTime").Note.Should().Be("No buzzer data in this stage.");
    }

    [Fact]
    public async Task TeamsThatNeverMet_AndAreLevelOnEverything_StayTied_WithTheReasonRecorded()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stage = await h.CreateStageAsync(("Mcq", 1));
        var teams = await h.CreateTeamsAsync(4);
        await h.ResetScoringDefaultsAsync();
        await h.SeedMcqAsync(2);
        var one = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, [teams[0], teams[1]]));
        var two = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 2, [teams[2], teams[3]]));
        await h.Client.PostAsync($"{one.Live}/end", null);
        await h.Client.PostAsync($"{two.Live}/end", null);

        var ordering = await OrderAsync(h, stage.Id, teams[0], teams[2]);

        ordering.IsFullyResolved.Should().BeFalse();
        ordering.RankedGroups.Should().ContainSingle().Which.Should().BeEquivalentTo([teams[0], teams[2]]);
        ordering.DecidingCriterion.Should().BeNull();
        ordering.Trace.Single(t => t.Criterion == "HeadToHead").Note.Should().Be("Teams never met.");
        ordering.Trace.Should().OnlyContain(t => !t.Separated);
    }
}
