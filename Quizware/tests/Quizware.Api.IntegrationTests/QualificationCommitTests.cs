using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>P11-07/08: committing a stage's qualification and rolling it back.</summary>
public class QualificationCommitTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public QualificationCommitTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Scenario(QualificationTestHarness Q, Guid StageId, Guid NextStageId, IReadOnlyList<Guid> Teams)
    {
        public string StageUrl => $"{Q.QualificationUrl}/stages/{StageId}";
    }

    /// <summary>Two 3-team matches (A 30, B 20, C 0 | D 30, E 10, F 0), a next
    /// stage of 2–3 teams per match, and 1 winner + 2 best remaining + 1
    /// wildcard slot. <paramref name="seeding"/> null uses the API (Manual seeding).</summary>
    private async Task<Scenario> CompletedStageAsync(SeedingMode? seeding = null)
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        var next = await q.CreateStageAsync(3);
        if (seeding is { } mode)
        {
            await q.SeedRuleAsync(stage.Id, 1, 2, mode, wildcardSlots: 1);
        }
        else
        {
            await q.SetRuleAsync(stage.Id, 1, 2, wildcardSlots: 1);
        }

        await q.SetTieBreakRuleAsync(stage.Id);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CCI"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));
        return new Scenario(q, stage.Id, next.Id, t);
    }

    private static async Task<int> CommitAsync(Scenario s)
    {
        var response = await s.Q.Client.PostAsync($"{s.StageUrl}/commit", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CommitQualificationResponse>())!.MatchesCreated;
    }

    private static Task<List<List<Guid>>> NextStageGroupsAsync(Scenario s) =>
        s.Q.Match.ReadDbAsync(async db =>
        {
            var matchIds = await db.Matches.Where(m => m.StageId == s.NextStageId).OrderBy(m => m.MatchNumber).Select(m => m.Id).ToListAsync();
            var participants = await db.MatchParticipants.Where(p => matchIds.Contains(p.MatchId)).ToListAsync();
            return matchIds.Select(id => participants.Where(p => p.MatchId == id).OrderBy(p => p.SeatNumber).Select(p => p.TeamId).ToList()).ToList();
        });

    [Fact]
    public async Task Commit_RecordsEveryQualifier_AndSeedsTheNextStage_PerTheSeedingMode()
    {
        var s = await CompletedStageAsync(SeedingMode.Snake);
        var t = s.Teams;
        (await s.Q.Client.PostAsJsonAsync($"{s.StageUrl}/wildcards", new SetWildcardsRequest([t[5]]))).EnsureSuccessStatusCode();

        var created = await CommitAsync(s);

        var rows = await s.Q.Match.ReadDbAsync(db => db.StageQualifications.Where(q => q.FromStageId == s.StageId).ToListAsync());
        rows.Should().OnlyContain(r => r.IsCommitted && r.ToStageId == s.NextStageId);
        rows.Select(r => (r.TeamId, r.QualificationReason)).Should().BeEquivalentTo(new[]
        {
            (t[0], QualificationReason.MatchWinner), (t[3], QualificationReason.MatchWinner),
            (t[1], QualificationReason.BestRemaining), (t[4], QualificationReason.BestRemaining), (t[5], QualificationReason.Manual),
        });

        // 5 qualifiers, matches of at most 3: two matches of 3 and 2, dealt
        // back and forth by stage rank. A and D share rank 1 (30 points).
        created.Should().Be(2);
        var groups = await NextStageGroupsAsync(s);
        groups.Select(g => g.Count).Should().Equal(3, 2);
        groups.SelectMany(g => g).Should().BeEquivalentTo([t[0], t[1], t[3], t[4], t[5]]);
        groups[0].Should().Contain(t[5], "snake order puts the lowest-ranked qualifier with the top seed");
        (await s.Q.Match.ReadDbAsync(db => db.Stages.SingleAsync(x => x.Id == s.NextStageId))).State.Should().Be(StageState.Ready);

        var preview = await s.Q.PreviewAsync(s.StageId);
        preview.CanCommit.Should().BeFalse();
        preview.BlockedReason.Should().Contain("already committed");
        var again = await s.Q.Client.PostAsync($"{s.StageUrl}/commit", null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Commit_WithManualSeeding_CreatesNoMatches_AndAutoSeedUsesTheCommittedTeams()
    {
        var s = await CompletedStageAsync();

        var created = await CommitAsync(s);
        var seed = await s.Q.Client.PostAsJsonAsync($"{s.Q.Match.MatchesUrl}/auto-seed", new AutoSeedMatchesRequest(s.NextStageId, "ByRank"));

        created.Should().Be(0);
        seed.EnsureSuccessStatusCode();
        var groups = await NextStageGroupsAsync(s);
        groups.SelectMany(g => g).Should().BeEquivalentTo([s.Teams[0], s.Teams[1], s.Teams[3], s.Teams[4]]);
    }

    [Fact]
    public async Task Commit_WhileATieOnAQualifyingPlaceIsOpen_IsRefusedWithUnresolvedTie()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, 1, 1);
        await q.SetTieBreakRuleAsync(stage.Id);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CII"), (t[5], "III"));

        var response = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("UNRESOLVED_TIE");
        (await q.Match.ReadDbAsync(db => db.StageQualifications.CountAsync(x => x.FromStageId == stage.Id && x.IsCommitted))).Should().Be(0);
        var ties = await q.Client.GetFromJsonAsync<TiesResponse>($"{q.QualificationUrl}/stages/{stage.Id}/ties");
        ties!.Ties.Should().BeEmpty("the refused commit saved nothing, its own detection included");
    }

    [Fact]
    public async Task Commit_RecordsTheTieThatDecidedAPlace()
    {
        // B and E level on 10 for one slot; E skipped instead of missing, so FewerIncorrect decides.
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, 1, 1);
        await q.SetTieBreakRuleAsync(stage.Id);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CIS"), (t[5], "III"));

        (await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/commit", null)).EnsureSuccessStatusCode();

        var row = await q.Match.ReadDbAsync(db => db.StageQualifications.SingleAsync(x => x.FromStageId == stage.Id && x.TeamId == t[4]));
        row.QualificationReason.Should().Be(QualificationReason.CriteriaTieBreak);
        row.TieBreakEventId.Should().NotBeNull();
        var tie = await q.Match.ReadDbAsync(db => db.TieBreakEvents.SingleAsync(e => e.Id == row.TieBreakEventId));
        tie.ResolvedByCriterion.Should().Be("FewerIncorrect");
    }

    [Fact]
    public async Task Rollback_RemovesTheCreatedMatchesAndCommittedRows_KeepingWildcardPicks_AndAllowsANewCommit()
    {
        var s = await CompletedStageAsync(SeedingMode.ByRank);
        (await s.Q.Client.PostAsJsonAsync($"{s.StageUrl}/wildcards", new SetWildcardsRequest([s.Teams[5]]))).EnsureSuccessStatusCode();
        await CommitAsync(s);

        var rollback = await s.Q.Client.PostAsync($"{s.StageUrl}/rollback", null);

        rollback.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NextStageGroupsAsync(s)).Should().BeEmpty();
        var rows = await s.Q.Match.ReadDbAsync(db => db.StageQualifications.Where(q => q.FromStageId == s.StageId).ToListAsync());
        rows.Should().ContainSingle().Which.Should().Match<Domain.Qualification.StageQualification>(
            r => r.TeamId == s.Teams[5] && r.QualificationReason == QualificationReason.Manual && !r.IsCommitted);
        (await s.Q.Match.ReadDbAsync(db => db.Stages.SingleAsync(x => x.Id == s.NextStageId))).State.Should().Be(StageState.Draft);
        var preview = await s.Q.PreviewAsync(s.StageId);
        preview.CanCommit.Should().BeTrue(preview.BlockedReason);
        preview.Qualifiers.Should().Contain(x => x.TeamId == s.Teams[5] && x.Reason == "Manual");
        (await CommitAsync(s)).Should().Be(2);
    }

    [Fact]
    public async Task Rollback_OnceTheNextStageHasStarted_IsRefused()
    {
        var s = await CompletedStageAsync(SeedingMode.ByRank);
        await CommitAsync(s);
        var firstNextMatch = await s.Q.Match.ReadDbAsync(db => db.Matches.Where(m => m.StageId == s.NextStageId).OrderBy(m => m.MatchNumber).FirstAsync());
        await s.Q.Match.SeedMcqAsync(3);
        (await s.Q.Client.PostAsync($"{MatchTestHarness.LiveUrl(firstNextMatch.Id)}/start", null)).EnsureSuccessStatusCode();

        var rollback = await s.Q.Client.PostAsync($"{s.StageUrl}/rollback", null);

        rollback.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await rollback.Content.ReadAsStringAsync()).Should().Contain("already started");
    }

    [Fact]
    public async Task Rollback_WithNothingCommitted_AndCommit_WithNoNextStage_AreConflicts()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var final = await q.CreateStageAsync(6);
        await q.SetRuleAsync(final.Id, 1, 0);
        var t = await q.Match.CreateTeamsAsync(2);
        await q.PlayMatchAsync(final.Id, (t[0], "CCC"), (t[1], "CCI"));

        var rollback = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{final.Id}/rollback", null);
        var commit = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{final.Id}/commit", null);

        rollback.StatusCode.Should().Be(HttpStatusCode.Conflict);
        commit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await commit.Content.ReadAsStringAsync()).Should().Contain("no next stage");
    }
}
