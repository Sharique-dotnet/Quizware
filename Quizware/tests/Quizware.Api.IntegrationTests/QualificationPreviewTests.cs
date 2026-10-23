using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Qualification;

namespace Quizware.Api.IntegrationTests;

/// <summary>P11-01/02/03 and wildcards: who advances, and why, before anything is committed.</summary>
public class QualificationPreviewTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public QualificationPreviewTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Two 3-team matches of 9 questions (3 turns each), a next stage,
    /// and the rule 1 winner per match + 2 best remaining + 1 wildcard slot.
    /// Final points: match 1 — A 30, B 20, C 0; match 2 — D 30, E 10, F 0.</summary>
    private static async Task<(QualificationTestHarness Q, Guid StageId, Dictionary<string, Guid> Teams)> TwoMatchStageAsync(
        CustomWebApplicationFactory factory, bool playSecondMatch = true)
    {
        var q = await QualificationTestHarness.CreateAsync(factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, winnersPerMatch: 1, bestRemaining: 2, wildcardSlots: 1);
        var ids = await q.Match.CreateTeamsAsync(6);
        var teams = new Dictionary<string, Guid> { ["A"] = ids[0], ["B"] = ids[1], ["C"] = ids[2], ["D"] = ids[3], ["E"] = ids[4], ["F"] = ids[5] };

        await q.PlayMatchAsync(stage.Id, (teams["A"], "CCC"), (teams["B"], "CCI"), (teams["C"], "III"));
        if (playSecondMatch)
        {
            await q.PlayMatchAsync(stage.Id, (teams["D"], "CCC"), (teams["E"], "CII"), (teams["F"], "III"));
        }
        else
        {
            await q.Match.CreateMatchAsync(stage.Id, 2, [teams["D"], teams["E"], teams["F"]]);
        }

        return (q, stage.Id, teams);
    }

    [Fact]
    public async Task Preview_WithAMatchStillToPlay_IsBlocked()
    {
        var (q, stageId, _) = await TwoMatchStageAsync(_factory, playSecondMatch: false);

        var preview = await q.PreviewAsync(stageId);

        preview.AllMatchesComplete.Should().BeFalse();
        preview.CanCommit.Should().BeFalse();
        preview.BlockedReason.Should().Contain("1 of 2 matches");
    }

    [Fact]
    public async Task Preview_ListsWinnersThenBestRemaining_AndRecordsATieBelowTheCutWithoutBlocking()
    {
        var (q, stageId, teams) = await TwoMatchStageAsync(_factory);

        var preview = await q.PreviewAsync(stageId);

        preview.AllMatchesComplete.Should().BeTrue();
        preview.Rule.TotalSlots.Should().Be(5);
        preview.Qualifiers.Select(x => (x.TeamId, x.Reason)).Should().BeEquivalentTo(new[]
        {
            (teams["A"], "MatchWinner"), (teams["D"], "MatchWinner"), (teams["B"], "BestRemaining"), (teams["E"], "BestRemaining"),
        });
        preview.Qualifiers.Single(x => x.TeamId == teams["B"]).SourceMatch.Should().Be("Match 1");
        preview.Eliminated.Select(e => e.TeamId).Should().BeEquivalentTo([teams["C"], teams["F"]]);
        preview.UnresolvedTies.Should().BeEmpty("C and F are level, but wholly below the cut");
        preview.CanCommit.Should().BeTrue(preview.BlockedReason);
        preview.ToStage.Should().NotBeNull();
    }

    [Fact]
    public async Task Wildcards_FillTheManualSlots_WithEliminatedTeamsOnly()
    {
        var (q, stageId, teams) = await TwoMatchStageAsync(_factory);
        var url = $"{q.QualificationUrl}/stages/{stageId}/wildcards";

        var tooMany = await q.Client.PostAsJsonAsync(url, new SetWildcardsRequest([teams["C"], teams["F"]]));
        var alreadyIn = await q.Client.PostAsJsonAsync(url, new SetWildcardsRequest([teams["A"]]));
        var ok = await q.Client.PostAsJsonAsync(url, new SetWildcardsRequest([teams["F"]]));

        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        alreadyIn.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await ok.Content.ReadFromJsonAsync<QualificationPreviewResponse>())!;
        preview.Qualifiers.Single(x => x.TeamId == teams["F"]).Reason.Should().Be("Manual");
        preview.Eliminated.Select(e => e.TeamId).Should().Equal(teams["C"]);

        var replaced = await q.Client.PostAsJsonAsync(url, new SetWildcardsRequest([teams["C"]]));
        var after = (await replaced.Content.ReadFromJsonAsync<QualificationPreviewResponse>())!;
        after.Qualifiers.Select(x => x.TeamId).Should().Contain(teams["C"]).And.NotContain(teams["F"]);
    }

    [Fact]
    public async Task ABoundaryTie_TheCriteriaSettle_DoesNotBlock_AndNamesTheReason()
    {
        // B and E are level on 10 for the last best-remaining slot; E skipped
        // a question instead of getting it wrong, so FewerIncorrect puts E ahead.
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, winnersPerMatch: 1, bestRemaining: 1);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CIS"), (t[5], "III"));

        var preview = await q.PreviewAsync(stage.Id);

        preview.UnresolvedTies.Should().BeEmpty();
        preview.Qualifiers.Single(x => x.TeamId == t[4]).Reason.Should().Be("CriteriaTieBreak");
        preview.Eliminated.Select(e => e.TeamId).Should().Contain(t[1]);
        preview.CanCommit.Should().BeTrue(preview.BlockedReason);
    }

    [Fact]
    public async Task ABoundaryTie_TheCriteriaCannotSettle_BlocksTheCommit_WithoutWritingAnything()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, winnersPerMatch: 1, bestRemaining: 1);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));

        var preview = await q.PreviewAsync(stage.Id);

        var tie = preview.UnresolvedTies.Should().ContainSingle().Subject;
        tie.Rank.Should().Be(3);
        tie.Teams.Select(x => x.TeamId).Should().BeEquivalentTo([t[1], t[4]]);
        tie.StillTied.Should().BeTrue();
        tie.CriteriaApplied.Should().Equal("TotalScore", "FewerIncorrect", "MoreCorrectAtHighDifficulty", "FasterAverageBuzzTime", "HeadToHead");
        tie.ResolutionOptions.Should().Contain(["TieBreakerMatch", "ManualDecision"]);
        preview.CanCommit.Should().BeFalse();
        preview.BlockedReason.Should().Contain("1 unresolved tie");
        preview.Qualifiers.Select(x => x.TeamId).Should().BeEquivalentTo([t[0], t[3]]);
        (await q.Match.ReadDbAsync(db => db.TieBreakEvents.CountAsync(e => e.StageId == stage.Id))).Should().Be(0);
    }

    [Fact]
    public async Task ATieForTheMatchWinnerSlot_IsDetectedPerMatch()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, winnersPerMatch: 1, bestRemaining: 0);
        var t = await q.Match.CreateTeamsAsync(3);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCI"), (t[1], "CCI"), (t[2], "III"));

        var preview = await q.PreviewAsync(stage.Id);

        preview.UnresolvedTies.Should().ContainSingle().Which.Teams.Select(x => x.TeamId).Should().BeEquivalentTo([t[0], t[1]]);
        preview.Qualifiers.Should().BeEmpty();
        preview.Eliminated.Select(e => e.TeamId).Should().Equal(t[2]);
    }

    [Fact]
    public async Task Preview_ForAStageWithoutAQualificationRule_IsAConflict()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(3);

        var response = await q.Client.GetAsync($"{q.QualificationUrl}/stages/{stage.Id}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("CONFLICT_STATE");
    }
}
