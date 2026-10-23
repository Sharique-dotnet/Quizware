using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Api.Contracts.V1.Standings;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>The tests docs/Implementation-Plan.md lists as required for Phase
/// 11. Every match is played through the live API; every progression step
/// comes from configuration (stages, rules, seeding modes) — none is hand-built.</summary>
public class Phase11RequiredTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public Phase11RequiredTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Plays every match of a stage in match-number order, giving each
    /// match's seats the scripts at the same position (seat 1 → scripts[i][0], …).</summary>
    private static async Task PlayStageAsync(QualificationTestHarness q, Guid stageId, params string[][] scriptsPerMatch)
    {
        var matches = await q.Match.ReadDbAsync(db => db.Matches
            .Where(m => m.StageId == stageId && m.MatchKind == MatchKind.Regular)
            .OrderBy(m => m.MatchNumber)
            .Select(m => m.Id)
            .ToListAsync());
        matches.Should().HaveCount(scriptsPerMatch.Length);

        for (var i = 0; i < matches.Count; i++)
        {
            var match = (await q.Client.GetFromJsonAsync<MatchDetailResponse>($"{q.Match.MatchesUrl}/{matches[i]}"))!;
            var seats = match.Participants.OrderBy(p => p.SeatNumber).ToList();
            await q.PlayAsync(match, seats.Select((p, seat) => (p.TeamId, scriptsPerMatch[i][seat])).ToArray());
        }
    }

    private static async Task<int> CommitAsync(QualificationTestHarness q, Guid stageId)
    {
        var response = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stageId}/commit", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CommitQualificationResponse>())!.MatchesCreated;
    }

    private static Task<List<Guid>> CommittedTeamsAsync(QualificationTestHarness q, Guid stageId) =>
        q.Match.ReadDbAsync(db => db.StageQualifications.Where(x => x.FromStageId == stageId && x.IsCommitted).Select(x => x.TeamId).ToListAsync());

    [Fact]
    public async Task EighteenTeams_ProgressNineThreeOne_FromConfigurationAlone()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        await q.Match.SeedMcqAsync(90);
        var league = await q.CreateStageAsync(9);
        var semi = await q.CreateStageAsync(9);
        var final = await q.CreateStageAsync(9);
        await q.SeedRuleAsync(league.Id, winnersPerMatch: 1, bestRemaining: 3, SeedingMode.Snake);
        await q.SeedRuleAsync(semi.Id, winnersPerMatch: 1, bestRemaining: 0, SeedingMode.ByRank);
        await q.SetTieBreakRuleAsync(league.Id);
        await q.SetTieBreakRuleAsync(semi.Id);
        await q.Match.CreateTeamsAsync(18);

        (await q.Client.PostAsJsonAsync($"{q.Match.MatchesUrl}/auto-seed", new AutoSeedMatchesRequest(league.Id, "ByRank"))).EnsureSuccessStatusCode();
        // Every League match has a clear winner (30); three runners-up on 20
        // take the best-remaining places, three on 10 and every 0 miss out.
        string[] strong = ["CCC", "CCI", "III"], weak = ["CCC", "CII", "III"];
        await PlayStageAsync(q, league.Id, strong, strong, strong, weak, weak, weak);
        var leaguePreview = await q.PreviewAsync(league.Id);
        var semiMatches = await CommitAsync(q, league.Id);

        await PlayStageAsync(q, semi.Id, strong, strong, strong);
        var finalMatches = await CommitAsync(q, semi.Id);

        await PlayStageAsync(q, final.Id, strong);

        leaguePreview.Qualifiers.Should().HaveCount(9);
        leaguePreview.Qualifiers.Count(x => x.Reason == "MatchWinner").Should().Be(6);
        leaguePreview.Qualifiers.Count(x => x.Reason == "BestRemaining").Should().Be(3);
        leaguePreview.UnresolvedTies.Should().BeEmpty();
        semiMatches.Should().Be(3);
        finalMatches.Should().Be(1);

        var leagueQualified = await CommittedTeamsAsync(q, league.Id);
        var semiQualified = await CommittedTeamsAsync(q, semi.Id);
        leagueQualified.Should().HaveCount(9);
        semiQualified.Should().HaveCount(3).And.BeSubsetOf(leagueQualified);
        var finalMatch = await q.Match.ReadDbAsync(db => db.Matches.SingleAsync(m => m.StageId == final.Id));
        finalMatch.State.Should().Be(MatchState.Completed);
        finalMatch.WinnerTeamId.Should().NotBeNull();
        semiQualified.Should().Contain(finalMatch.WinnerTeamId!.Value);
    }

    /// <summary>A League of six 3-team matches; 1 winner each + 3 best remaining = 9.
    /// Runners-up: two on 20 (7th and 8th), two on 10 contesting 9th, two on 0.</summary>
    private async Task<(QualificationTestHarness Q, Guid LeagueId, Guid NinthA, Guid NinthB)> NinthPlaceTieAsync(string fourthRunnerUp)
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        await q.Match.SeedMcqAsync(60);
        var league = await q.CreateStageAsync(9);
        await q.CreateStageAsync(9);
        await q.SeedRuleAsync(league.Id, winnersPerMatch: 1, bestRemaining: 3, SeedingMode.Snake);
        await q.SetTieBreakRuleAsync(league.Id);
        await q.Match.CreateTeamsAsync(18);
        (await q.Client.PostAsJsonAsync($"{q.Match.MatchesUrl}/auto-seed", new AutoSeedMatchesRequest(league.Id, "ByRank"))).EnsureSuccessStatusCode();

        await PlayStageAsync(q, league.Id,
            ["CCC", "CCI", "III"], ["CCC", "CCI", "III"], ["CCC", "CII", "III"], ["CCC", fourthRunnerUp, "III"], ["CCC", "III", "III"], ["CCC", "III", "III"]);
        var seat2 = await q.Match.ReadDbAsync(db => (
                from m in db.Matches
                where m.StageId == league.Id
                join p in db.MatchParticipants on m.Id equals p.MatchId
                where p.SeatNumber == 2
                orderby m.MatchNumber
                select p.TeamId)
            .ToListAsync());
        return (q, league.Id, seat2[2], seat2[3]);
    }

    [Fact]
    public async Task ANinthTenthWildcardTie_TheCriteriaSettle_RecordsTheDecidingCriterion()
    {
        var (q, leagueId, ninthA, ninthB) = await NinthPlaceTieAsync(fourthRunnerUp: "CIS");

        await CommitAsync(q, leagueId);

        var tie = await q.Match.ReadDbAsync(db => db.TieBreakEvents.SingleAsync(e => e.StageId == leagueId && e.ContestedSlots == 1));
        tie.ContestedRank.Should().Be(9);
        tie.ResolutionMethod.Should().Be(TieBreakResolutionMethod.Criteria);
        tie.ResolvedByCriterion.Should().Be("FewerIncorrect", "TotalScore runs first and cannot separate them; FewerIncorrect is next");
        (await CommittedTeamsAsync(q, leagueId)).Should().HaveCount(9).And.Contain(ninthB).And.NotContain(ninthA);
    }

    [Fact]
    public async Task ANinthTenthWildcardTie_TheCriteriaCannotSettle_IsPlayedOffByOnlyTheTwoTiedTeams()
    {
        var (q, leagueId, ninthA, ninthB) = await NinthPlaceTieAsync(fourthRunnerUp: "CII");

        var blocked = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{leagueId}/commit", null);
        var ties = await (await q.Client.PostAsync($"{q.QualificationUrl}/stages/{leagueId}/ties/detect", null)).Content.ReadFromJsonAsync<TiesResponse>();
        var tie = ties!.Ties.Single(x => x.AffectsQualification);
        var created = await q.Client.PostAsync($"{q.QualificationUrl}/ties/{tie.TieBreakEventId}/tie-break-match", null);
        var tieBreak = (await created.Content.ReadFromJsonAsync<CreateTieBreakMatchResponse>())!;
        var match = (await q.Client.GetFromJsonAsync<MatchDetailResponse>($"{q.Match.MatchesUrl}/{tieBreak.MatchId}"))!;
        await q.PlayAsync(match, (ninthA, "C"), (ninthB, "I"));
        var created9 = await CommitAsync(q, leagueId);

        (await MatchTestHarness.ErrorCodeAsync(blocked)).Should().Be("UNRESOLVED_TIE");
        tie.ContestedRank.Should().Be(9);
        tie.CriteriaApplied.Select(c => c.Criterion).Should().Equal("TotalScore", "FewerIncorrect", "HeadToHead");
        tie.CriteriaApplied.Should().OnlyContain(c => !c.Separated);
        tieBreak.Participants.Select(p => p.TeamId).Should().BeEquivalentTo([ninthA, ninthB]);
        tieBreak.MatchKind.Should().Be("TieBreak");
        created9.Should().Be(3);
        var row = await q.Match.ReadDbAsync(db => db.StageQualifications.SingleAsync(x => x.FromStageId == leagueId && x.TeamId == ninthA));
        row.QualificationReason.Should().Be(QualificationReason.TieBreakWin);
        row.TieBreakEventId.Should().Be(tie.TieBreakEventId);
        (await CommittedTeamsAsync(q, leagueId)).Should().HaveCount(9).And.NotContain(ninthB);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TieBreakPoints_MoveTheStageLeaderboard_OnlyWhenTheRuleSaysSo(bool scoreCountsTowardStage)
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, 1, 1);
        await q.SetTieBreakRuleAsync(stage.Id, scoreCountsTowardStage: scoreCountsTowardStage);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));
        var tie = (await (await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/ties/detect", null))
            .Content.ReadFromJsonAsync<TiesResponse>())!.Ties.Single(x => x.AffectsQualification);
        await q.Match.SeedMcqAsync(2);
        var tieBreak = (await (await q.Client.PostAsync($"{q.QualificationUrl}/ties/{tie.TieBreakEventId}/tie-break-match", null))
            .Content.ReadFromJsonAsync<CreateTieBreakMatchResponse>())!;

        await q.PlayAsync((await q.Client.GetFromJsonAsync<MatchDetailResponse>($"{q.Match.MatchesUrl}/{tieBreak.MatchId}"))!, (t[1], "C"), (t[4], "I"));

        var standings = (await q.Client.GetFromJsonAsync<StageStandingsResponse>($"/api/v1/programs/{q.Match.ProgramId}/standings/stages/{stage.Id}"))!;
        standings.Standings.Single(x => x.TeamId == t[1]).Score.Should().Be(scoreCountsTowardStage ? 20 : 10);
        standings.Standings.Single(x => x.TeamId == t[4]).Score.Should().Be(10);
    }

    [Fact]
    public async Task ATieBelowTheCut_DoesNotBlockTheCommit()
    {
        // Teams 3 and 6 are level on 0, but nowhere near a qualifying place.
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, 1, 2);
        await q.SetTieBreakRuleAsync(stage.Id);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CCI"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));

        await CommitAsync(q, stage.Id);

        var below = await q.Match.ReadDbAsync(db => db.TieBreakEvents.SingleAsync(e => e.StageId == stage.Id));
        below.ContestedSlots.Should().Be(0, "it was recorded, as a tie that contests no place");
        (await CommittedTeamsAsync(q, stage.Id)).Should().BeEquivalentTo([t[0], t[3], t[1], t[4]]);
    }

    [Fact]
    public async Task ResolveManually_IsForbiddenToEveryRoleButProgramAdminAndSuperAdmin()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, 1, 1);
        await q.SetTieBreakRuleAsync(stage.Id);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));
        var tie = (await (await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/ties/detect", null))
            .Content.ReadFromJsonAsync<TiesResponse>())!.Ties.Single(x => x.AffectsQualification);
        var url = $"{q.QualificationUrl}/ties/{tie.TieBreakEventId}/resolve-manually";
        var body = new ResolveTieManuallyRequest([t[1], t[4]], "ManualDecision", "Team 5 withdrew.", Guid.NewGuid());

        foreach (var role in new[] { Roles.Operator, Roles.Scorer, Roles.QuestionAuthor, Roles.Auditor, Roles.Display })
        {
            var client = await q.Match.CreateClientAsync(role);
            (await client.PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, role);
        }

        var programAdmin = await q.Match.CreateClientAsync(Roles.ProgramAdmin);
        (await programAdmin.PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResolveManually_IsAllowedForASuperAdmin()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, 1, 1);
        await q.SetTieBreakRuleAsync(stage.Id);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));
        var tie = (await (await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/ties/detect", null))
            .Content.ReadFromJsonAsync<TiesResponse>())!.Ties.Single(x => x.AffectsQualification);
        var superAdmin = await q.Match.CreateClientAsync(Roles.SuperAdmin);

        var response = await superAdmin.PostAsJsonAsync(
            $"{q.QualificationUrl}/ties/{tie.TieBreakEventId}/resolve-manually",
            new ResolveTieManuallyRequest([t[4], t[1]], "CoinToss", "Coin toss witnessed by both captains.", Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await q.Match.ReadDbAsync(db => db.TieBreakEvents.SingleAsync(e => e.Id == tie.TieBreakEventId)))
            .ResolutionMethod.Should().Be(TieBreakResolutionMethod.CoinToss);
    }
}
