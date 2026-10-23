using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>P11-04/05/06: a tie the criteria cannot settle is played out as an
/// ordinary match, or settled by an audited manual decision.</summary>
public class QualificationTieBreakTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public QualificationTieBreakTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Scenario(QualificationTestHarness Q, Guid StageId, IReadOnlyList<Guid> Teams, TieDto Tie)
    {
        public Guid Second => Teams[1];

        public Guid Fifth => Teams[4];

        public string TieUrl => $"{Q.QualificationUrl}/ties/{Tie.TieBreakEventId}";
    }

    /// <summary>Teams 2 and 5 level on 10 for the single best-remaining slot,
    /// on every criterion; the tie is detected and waiting for play.</summary>
    private async Task<Scenario> UnsettledTieAsync(int maxExtraRounds = 3, string onStillTied = "ManualDecision")
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, winnersPerMatch: 1, bestRemaining: 1);
        await q.SetTieBreakRuleAsync(stage.Id, maxExtraRounds: maxExtraRounds, onStillTied: onStillTied);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));
        var detect = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/ties/detect", null);
        detect.EnsureSuccessStatusCode();
        var tie = (await detect.Content.ReadFromJsonAsync<TiesResponse>())!.Ties.Single(x => x.AffectsQualification);
        return new Scenario(q, stage.Id, t, tie);
    }

    private static async Task<CreateTieBreakMatchResponse> CreateTieBreakAsync(Scenario s)
    {
        await s.Q.Match.SeedMcqAsync(2);
        var response = await s.Q.Client.PostAsync($"{s.TieUrl}/tie-break-match", null);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CreateTieBreakMatchResponse>())!;
    }

    private static async Task PlayTieBreakAsync(Scenario s, CreateTieBreakMatchResponse created, string secondScript, string fifthScript)
    {
        var match = (await s.Q.Client.GetFromJsonAsync<MatchDetailResponse>($"{s.Q.Match.MatchesUrl}/{created.MatchId}"))!;
        await s.Q.PlayAsync(match, (s.Second, secondScript), (s.Fifth, fifthScript));
    }

    private static Task<TieDto?> TieAsync(Scenario s) => s.Q.Client.GetFromJsonAsync<TieDto>(s.TieUrl);

    [Fact]
    public async Task TheTieBreakMatch_HasOnlyTheTiedTeams_AndItsResultResolvesTheTie_WithoutMovingTheStageScores()
    {
        var s = await UnsettledTieAsync();

        var created = await CreateTieBreakAsync(s);

        created.MatchKind.Should().Be("TieBreak");
        created.State.Should().Be("Ready", created.NextStep);
        created.Name.Should().Contain("Tie-break for 3rd place");
        created.Participants.Select(p => p.TeamId).Should().BeEquivalentTo([s.Second, s.Fifth]);
        created.Segments.Should().ContainSingle().Which.Should().Match<TieBreakSegmentDto>(x => x.Format == "Mcq" && x.PlannedQuestionCount == 2);
        created.ScoreCountsTowardStage.Should().BeFalse();
        (await TieAsync(s))!.State.Should().Be("InProgress");

        await PlayTieBreakAsync(s, created, "C", "I");

        var tie = (await TieAsync(s))!;
        tie.State.Should().Be("Resolved");
        tie.AvailableActions.Should().BeEmpty();
        var record = await s.Q.Match.ReadDbAsync(db => db.TieBreakEvents.SingleAsync(e => e.Id == s.Tie.TieBreakEventId));
        record.ResolutionMethod.Should().Be(TieBreakResolutionMethod.PlayedTieBreak);
        var ranks = await s.Q.Match.ReadDbAsync(db => db.TieBreakParticipants.Where(p => p.TieBreakEventId == s.Tie.TieBreakEventId).ToListAsync());
        ranks.Select(p => (p.TeamId, p.TieBreakPoints, p.ResultRank, p.Qualified))
            .Should().BeEquivalentTo(new[] { (s.Second, 10, (int?)1, true), (s.Fifth, 0, (int?)2, false) });

        var preview = await s.Q.PreviewAsync(s.StageId);
        preview.Qualifiers.Single(x => x.TeamId == s.Second).Reason.Should().Be("TieBreakWin");
        preview.Eliminated.Select(e => e.TeamId).Should().Contain(s.Fifth);
        preview.Qualifiers.Single(x => x.TeamId == s.Second).StageScore.Should().Be(10, "tie-break points do not count toward the stage");
        preview.CanCommit.Should().BeTrue(preview.BlockedReason);
        (await s.Q.Match.ReadDbAsync(db => db.OutboxMessages.CountAsync(m => m.Type == "TieBreakResolved" && m.PayloadJson.Contains(s.Tie.TieBreakEventId.ToString()))))
            .Should().Be(1);
    }

    [Fact]
    public async Task ALevelRound_WaitsForAnotherRound_UntilTheExtraRoundsRunOut_ThenOnlyAManualDecisionRemains()
    {
        var s = await UnsettledTieAsync(maxExtraRounds: 1);

        await PlayTieBreakAsync(s, await CreateTieBreakAsync(s), "C", "C");
        var afterFirst = (await TieAsync(s))!;
        var second = await CreateTieBreakAsync(s);
        await PlayTieBreakAsync(s, second, "I", "I");
        var afterSecond = (await TieAsync(s))!;
        await s.Q.Match.SeedMcqAsync(2);
        var third = await s.Q.Client.PostAsync($"{s.TieUrl}/tie-break-match", null);

        afterFirst.State.Should().Be("AwaitingPlay");
        afterFirst.AvailableActions.Should().Contain("CreateTieBreakMatch");
        second.Name.Should().Contain("round 2");
        afterSecond.State.Should().Be("AwaitingPlay");
        afterSecond.AvailableActions.Should().Equal("ResolveManually", "Abandon");
        third.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(third)).Should().Be("TIE_BREAK_NOT_APPLICABLE");
        (await s.Q.PreviewAsync(s.StageId)).CanCommit.Should().BeFalse();
    }

    [Fact]
    public async Task StillLevel_WhenTheRuleSharesTheSlot_BothTeamsQualify()
    {
        var s = await UnsettledTieAsync(maxExtraRounds: 0, onStillTied: "ShareTheSlot");

        await PlayTieBreakAsync(s, await CreateTieBreakAsync(s), "C", "C");

        (await TieAsync(s))!.State.Should().Be("Resolved");
        var record = await s.Q.Match.ReadDbAsync(db => db.TieBreakEvents.SingleAsync(e => e.Id == s.Tie.TieBreakEventId));
        record.ResolutionMethod.Should().Be(TieBreakResolutionMethod.Shared);
        var preview = await s.Q.PreviewAsync(s.StageId);
        preview.Qualifiers.Select(x => x.TeamId).Should().Contain([s.Second, s.Fifth]);
    }

    [Fact]
    public async Task ResolveManually_RanksTheContestedTeams_WithAReasonAndApprover_AndWithdrawsAnUnstartedTieBreak()
    {
        var s = await UnsettledTieAsync();
        var created = await CreateTieBreakAsync(s);
        var approver = Guid.NewGuid();
        var url = $"{s.TieUrl}/resolve-manually";

        var wrongTeams = await s.Q.Client.PostAsJsonAsync(url, new ResolveTieManuallyRequest([s.Second], "ManualDecision", "x", approver));
        var noReason = await s.Q.Client.PostAsJsonAsync(url, new ResolveTieManuallyRequest([s.Fifth, s.Second], "ManualDecision", "", approver));
        var noSharing = await s.Q.Client.PostAsJsonAsync(url, new ResolveTieManuallyRequest([s.Fifth, s.Second], "ShareTheSlot", "Both deserve it", approver));
        var ok = await s.Q.Client.PostAsJsonAsync(url, new ResolveTieManuallyRequest(
            [s.Fifth, s.Second], "ManualDecision", "Team 2 withdrew from the tie-break; team 5 advances.", approver));
        var again = await s.Q.Client.PostAsJsonAsync(url, new ResolveTieManuallyRequest(
            [s.Fifth, s.Second], "ManualDecision", "Again", approver));

        wrongTeams.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        noSharing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        (await ok.Content.ReadFromJsonAsync<TieDto>())!.State.Should().Be("Resolved");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(again)).Should().Be("TIE_BREAK_NOT_APPLICABLE");

        var record = await s.Q.Match.ReadDbAsync(db => db.TieBreakEvents.SingleAsync(e => e.Id == s.Tie.TieBreakEventId));
        record.ResolutionMethod.Should().Be(TieBreakResolutionMethod.Manual);
        record.ApprovedByUserId.Should().Be(approver);
        record.Notes.Should().Contain("withdrew");
        (await s.Q.Match.ReadDbAsync(db => db.Matches.AnyAsync(m => m.Id == created.MatchId))).Should().BeFalse("the unstarted tie-break was withdrawn");
        var preview = await s.Q.PreviewAsync(s.StageId);
        preview.Qualifiers.Single(x => x.TeamId == s.Fifth).Reason.Should().Be("Manual");
        preview.Eliminated.Select(e => e.TeamId).Should().Contain(s.Second);
    }

    [Fact]
    public async Task AbandoningTheTieBreakMatch_SendsTheTieBackToWaitingForOne()
    {
        var s = await UnsettledTieAsync();
        var created = await CreateTieBreakAsync(s);
        var live = MatchTestHarness.LiveUrl(created.MatchId);
        (await s.Q.Client.PostAsync($"{live}/start", null)).EnsureSuccessStatusCode();

        var abandonTieWhilePlaying = await s.Q.Client.PostAsJsonAsync($"{s.TieUrl}/abandon", new AbandonTieRequest("No longer needed"));
        (await s.Q.Client.PostAsJsonAsync($"{live}/abandon", new Contracts.V1.LiveMatch.AbandonMatchRequest("Power cut"))).EnsureSuccessStatusCode();

        abandonTieWhilePlaying.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var tie = (await TieAsync(s))!;
        tie.State.Should().Be("AwaitingPlay");
        tie.AvailableActions.Should().Contain("CreateTieBreakMatch");
    }

    [Fact]
    public async Task AbandonTie_CancelsIt_AndTheNextDetectionRecordsItAfresh()
    {
        var s = await UnsettledTieAsync();

        var abandon = await s.Q.Client.PostAsJsonAsync($"{s.TieUrl}/abandon", new AbandonTieRequest("Standings under review"));
        var listed = await s.Q.Client.GetFromJsonAsync<TiesResponse>($"{s.Q.QualificationUrl}/stages/{s.StageId}/ties");
        var redetected = await s.Q.Client.PostAsync($"{s.Q.QualificationUrl}/stages/{s.StageId}/ties/detect", null);

        abandon.StatusCode.Should().Be(HttpStatusCode.NoContent);
        listed!.Ties.Should().NotContain(x => x.TieBreakEventId == s.Tie.TieBreakEventId);
        var fresh = (await redetected.Content.ReadFromJsonAsync<TiesResponse>())!.Ties.Single(x => x.AffectsQualification);
        fresh.TieBreakEventId.Should().NotBe(s.Tie.TieBreakEventId);
        fresh.State.Should().Be("AwaitingPlay");
    }

    [Fact]
    public async Task ATieBelowTheCut_CannotBePlayedOff()
    {
        var s = await UnsettledTieAsync();
        var below = (await s.Q.Client.GetFromJsonAsync<TiesResponse>($"{s.Q.QualificationUrl}/stages/{s.StageId}/ties"))!
            .Ties.Single(x => !x.AffectsQualification);

        var response = await s.Q.Client.PostAsync($"{s.Q.QualificationUrl}/ties/{below.TieBreakEventId}/tie-break-match", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("TIE_BREAK_NOT_APPLICABLE");
    }
}
