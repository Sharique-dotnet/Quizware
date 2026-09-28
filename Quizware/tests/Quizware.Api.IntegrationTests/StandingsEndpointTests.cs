using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Scores;
using Quizware.Api.Contracts.V1.Standings;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 10d: stage, overall and team standings from the stored
/// score totals.</summary>
public class StandingsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public StandingsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string StandingsUrl(MatchTestHarness h) => $"/api/v1/programs/{h.ProgramId}/standings";

    private static Task<HttpResponseMessage> AdjustAsync(MatchTestHarness h, Guid matchId, Guid teamId, int points) =>
        h.Client.PostAsJsonAsync($"/api/v1/matches/{matchId}/scores/adjust", new AdjustScoreRequest(teamId, points, "Test"));

    private static async Task<StageStandingsResponse> StageAsync(MatchTestHarness h, Guid stageId) =>
        (await h.Client.GetFromJsonAsync<StageStandingsResponse>($"{StandingsUrl(h)}/stages/{stageId}"))!;

    [Fact]
    public async Task Stage_RanksTeamsByStagePoints()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 2));
        var teams = started.Match.Participants.Select(p => p.TeamId).ToList();
        await AdjustAsync(h, started.Match.Id, teams[0], 10);
        await AdjustAsync(h, started.Match.Id, teams[1], 5);

        var standings = await StageAsync(h, started.Match.StageId);

        standings.Standings.Select(s => (s.TeamId, s.Score, s.Rank)).Should().Equal((teams[0], 10, 1), (teams[1], 5, 2), (teams[2], 0, 3));
        standings.TieBreakNotes.Should().BeEmpty();
    }

    [Fact]
    public async Task Stage_OrdersTeamsLevelOnPointsByTheTieBreakCriteria_AndExplainsIt()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var first = await h.ServeAsync(started);
        var clean = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, first.MatchQuestionId, clean, "Correct");
        var second = await h.ServeAsync(started);
        var sloppy = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, second.MatchQuestionId, sloppy, "Incorrect");
        var cleanTeam = (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == clean))).TeamId;
        var sloppyTeam = (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == sloppy))).TeamId;
        await AdjustAsync(h, started.Match.Id, sloppyTeam, 10);

        var standings = await StageAsync(h, started.Match.StageId);

        standings.Standings.Select(s => (s.TeamId, s.Rank)).Should().Equal((cleanTeam, 1), (sloppyTeam, 2));
        standings.TieBreakNotes.Should().ContainSingle().Which.Should().Contain("Level on 10 points").And.Contain("decided by FewerIncorrect");
    }

    [Fact]
    public async Task Stage_TeamsStillLevelAfterEveryCriterion_ShareTheirRank()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 2));
        var teams = started.Match.Participants.Select(p => p.TeamId).ToList();
        await AdjustAsync(h, started.Match.Id, teams[2], 4);

        var standings = await StageAsync(h, started.Match.StageId);

        standings.Standings[0].Should().Match<StandingEntry>(s => s.TeamId == teams[2] && s.Rank == 1);
        standings.Standings.Skip(1).Should().OnlyContain(s => s.Rank == 2);
        standings.TieBreakNotes.Should().ContainSingle().Which.Should().Contain("still level after every criterion").And.Contain("Teams never met");
    }

    [Fact]
    public async Task Stage_OfAnotherProgram_IsNotFound_AndTheStagesRouteAgrees()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await AdjustAsync(h, started.Match.Id, started.Match.Participants[0].TeamId, 3);

        var unknown = await h.Client.GetAsync($"{StandingsUrl(h)}/stages/{Guid.NewGuid()}");
        var viaStages = (await h.Client.GetFromJsonAsync<StageStandingsResponse>(
            $"/api/v1/programs/{h.ProgramId}/stages/{started.Match.StageId}/standings"))!;

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        viaStages.Standings.Should().BeEquivalentTo((await StageAsync(h, started.Match.StageId)).Standings);
    }

    [Fact]
    public async Task DisqualifiedAndExcluded_LeavesTheStandings_ButKeepsItsHistory()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var question = await h.ServeAsync(started);
        var scorer = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, question.MatchQuestionId, scorer, "Correct");
        var scorerTeam = (await h.ReadDbAsync(db => db.MatchParticipants.SingleAsync(p => p.Id == scorer))).TeamId;

        await h.DisqualifyAsync(started, scorer, excludeFromStandings: true);

        (await StageAsync(h, started.Match.StageId)).Standings.Should().NotContain(s => s.TeamId == scorerTeam).And.HaveCount(2);
        (await h.Client.GetFromJsonAsync<OverallStandingsResponse>($"{StandingsUrl(h)}/overall"))!.Standings
            .Should().NotContain(s => s.TeamId == scorerTeam);
        var record = (await h.Client.GetFromJsonAsync<TeamStandingResponse>($"{StandingsUrl(h)}/teams/{scorerTeam}"))!;
        record.Matches.Should().ContainSingle().Which.Score.Should().Be(10);
        record.OverallRank.Should().Be(0);
    }

    [Fact]
    public async Task DisqualifiedButStillStanding_StaysInTheStandings()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var removed = started.Match.Participants[0];

        await h.DisqualifyAsync(started, removed.Id, excludeFromStandings: false);

        (await StageAsync(h, started.Match.StageId)).Standings.Should().Contain(s => s.TeamId == removed.TeamId);
    }

    [Fact]
    public async Task Overall_SumsEveryStage_AndTheTeamRecordListsEachMatch()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedMcqAsync(2);
        var teams = await h.CreateTeamsAsync(2);
        var league = await h.CreateStageAsync(("Mcq", 1));
        var final = await h.CreateStageAsync(("Mcq", 1));
        var first = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(league.Id, 1, teams));
        await AdjustAsync(h, first.Match.Id, teams[0], 10);
        await h.Client.PostAsync($"{first.Live}/end", null);
        var second = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(final.Id, 1, teams));
        await AdjustAsync(h, second.Match.Id, teams[1], 25);
        await h.Client.PostAsync($"{second.Live}/end", null);

        var overall = (await h.Client.GetFromJsonAsync<OverallStandingsResponse>($"{StandingsUrl(h)}/overall"))!.Standings;
        var record = (await h.Client.GetFromJsonAsync<TeamStandingResponse>($"{StandingsUrl(h)}/teams/{teams[0]}"))!;

        overall.Select(s => (s.TeamId, s.Score, s.Rank)).Should().Equal((teams[1], 25, 1), (teams[0], 10, 2));
        record.Matches.Select(m => (m.MatchId, m.Score, m.Rank)).Should().Equal((first.Match.Id, 10, 1), (second.Match.Id, 0, 2));
        record.OverallScore.Should().Be(10);
        record.OverallRank.Should().Be(2);
    }

    [Fact]
    public async Task Team_OfAnotherProgram_IsNotFound()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);

        var response = await h.Client.GetAsync($"{StandingsUrl(h)}/teams/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
