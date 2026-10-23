using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quizware.Api.Contracts.V1.Qualification;
using Quizware.Api.Contracts.V1.Scores;

namespace Quizware.Api.IntegrationTests;

/// <summary>P11-02/03: detecting ties saves them, with the criteria phase applied.</summary>
public class QualificationTiesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public QualificationTiesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Two 3-team matches; rule 1 winner + 1 best remaining. Team 1
    /// and team 4 win; teams 2 and 5 are level on 10 for the last slot.
    /// <paramref name="fifthScript"/> "CII" leaves them level on every
    /// criterion; "CIS" lets FewerIncorrect put team 5 ahead.</summary>
    private static async Task<(QualificationTestHarness Q, Guid StageId, IReadOnlyList<Guid> Teams, IReadOnlyList<Guid> Matches)> BoundaryTieAsync(
        CustomWebApplicationFactory factory, string fifthScript = "CII", bool withTieBreakRule = true)
    {
        var q = await QualificationTestHarness.CreateAsync(factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, winnersPerMatch: 1, bestRemaining: 1);
        if (withTieBreakRule)
        {
            await q.SetTieBreakRuleAsync(stage.Id);
        }

        var t = await q.Match.CreateTeamsAsync(6);
        var first = await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        var second = await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], fifthScript), (t[5], "III"));
        return (q, stage.Id, t, [first.Id, second.Id]);
    }

    private static async Task<TiesResponse> DetectAsync(QualificationTestHarness q, Guid stageId)
    {
        var response = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stageId}/ties/detect", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TiesResponse>())!;
    }

    [Fact]
    public async Task Detect_AnUnsettledBoundaryTie_SavesIt_AwaitingPlay_AndIsIdempotent()
    {
        var (q, stageId, t, _) = await BoundaryTieAsync(_factory);

        var first = await DetectAsync(q, stageId);
        var again = await DetectAsync(q, stageId);

        first.BlocksCommit.Should().BeTrue();
        first.Ties.Should().HaveCount(2, "the boundary tie, plus teams 3 and 6 level on 0 below the cut");
        var tie = first.Ties.Single(x => x.AffectsQualification);
        tie.State.Should().Be("AwaitingPlay");
        tie.AffectsQualification.Should().BeTrue();
        tie.ContestedRank.Should().Be(3);
        tie.ContestedSlots.Should().Be(1);
        tie.Teams.Select(x => (x.TeamId, x.EnteringScore)).Should().BeEquivalentTo(new[] { (t[1], 10), (t[4], 10) });
        tie.CriteriaApplied.Select(c => c.Criterion).Should().Equal("TotalScore", "FewerIncorrect", "HeadToHead");
        tie.CriteriaApplied.Should().OnlyContain(c => !c.Separated);
        tie.CriteriaApplied.Single(c => c.Criterion == "FewerIncorrect").Values.Should().Equal(2, 2);
        tie.CriteriaApplied.Single(c => c.Criterion == "HeadToHead").Reason.Should().NotBeNullOrEmpty();
        tie.Rule.TieBreakFormat.Should().Be("Mcq");
        tie.AvailableActions.Should().Equal("CreateTieBreakMatch", "ResolveManually", "Abandon");
        again.Ties.Select(x => x.TieBreakEventId).Should().BeEquivalentTo(first.Ties.Select(x => x.TieBreakEventId));

        var single = await q.Client.GetFromJsonAsync<TieDto>($"{q.QualificationUrl}/ties/{tie.TieBreakEventId}");
        single!.State.Should().Be("AwaitingPlay");
        var listed = await q.Client.GetFromJsonAsync<TiesResponse>($"{q.QualificationUrl}/stages/{stageId}/ties");
        listed!.Ties.Should().HaveCount(2);
    }

    [Fact]
    public async Task Detect_ATieTheCriteriaSettle_IsResolved_WithTheDecidingCriterion()
    {
        var (q, stageId, t, _) = await BoundaryTieAsync(_factory, fifthScript: "CIS");

        var ties = await DetectAsync(q, stageId);

        ties.BlocksCommit.Should().BeFalse();
        var tie = ties.Ties.Single(x => x.ContestedSlots == 1);
        tie.State.Should().Be("Resolved");
        tie.AvailableActions.Should().BeEmpty();
        tie.CriteriaApplied.Single(c => c.Criterion == "FewerIncorrect").Separated.Should().BeTrue();
        var record = await q.Match.ReadDbAsync(db => Task.FromResult(db.TieBreakEvents.Single(e => e.Id == tie.TieBreakEventId)));
        record.ResolvedByCriterion.Should().Be("FewerIncorrect");
        record.ResolutionMethod.Should().Be(Domain.Enums.TieBreakResolutionMethod.Criteria);
        var preview = await q.PreviewAsync(stageId);
        preview.Qualifiers.Single(x => x.TeamId == t[4]).Reason.Should().Be("CriteriaTieBreak");
        preview.CanCommit.Should().BeTrue(preview.BlockedReason);
    }

    [Fact]
    public async Task Detect_ATieBelowTheCut_IsRecordedButDoesNotBlock()
    {
        // Teams 3 and 6 are level on 0, wholly below the cut.
        var (q, stageId, t, _) = await BoundaryTieAsync(_factory, fifthScript: "CIS");

        var ties = await DetectAsync(q, stageId);

        ties.Ties.Should().HaveCount(2);
        var below = ties.Ties.Single(x => x.Teams.Any(team => team.TeamId == t[2]));
        below.AffectsQualification.Should().BeFalse();
        below.ContestedSlots.Should().Be(0);
        below.AvailableActions.Should().BeEmpty();
        ties.BlocksCommit.Should().BeFalse();
    }

    [Fact]
    public async Task Detect_BeforeEveryMatchIsCompleted_IsAConflict()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.SetRuleAsync(stage.Id, 1, 1);
        var t = await q.Match.CreateTeamsAsync(3);
        await q.Match.CreateMatchAsync(stage.Id, 1, t);

        var response = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/ties/detect", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Detect_ATieThatMatters_WithNoTieBreakRule_AsksForOne()
    {
        var (q, stageId, _, _) = await BoundaryTieAsync(_factory, withTieBreakRule: false);

        var response = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stageId}/ties/detect", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("rules/tie-break");
    }

    [Fact]
    public async Task ATieThatDisappears_IsAbandonedOnTheNextDetection()
    {
        var (q, stageId, t, matches) = await BoundaryTieAsync(_factory);
        var tie = (await DetectAsync(q, stageId)).Ties.Single(x => x.AffectsQualification);

        var adjust = await q.Client.PostAsJsonAsync(
            $"/api/v1/matches/{matches[1]}/scores/adjust", new AdjustScoreRequest(t[4], 5, "Judges' review"));
        adjust.EnsureSuccessStatusCode();
        var after = await DetectAsync(q, stageId);

        after.Ties.Should().NotContain(x => x.TieBreakEventId == tie.TieBreakEventId);
        after.BlocksCommit.Should().BeFalse();
        var record = await q.Match.ReadDbAsync(db => Task.FromResult(db.TieBreakEvents.Single(e => e.Id == tie.TieBreakEventId)));
        record.State.Should().Be(Domain.Enums.TieBreakEventState.Abandoned);
        (await q.PreviewAsync(stageId)).Qualifiers.Single(x => x.TeamId == t[4]).Reason.Should().Be("BestRemaining");
    }

    [Fact]
    public async Task GetTie_ForAnUnknownTie_IsNotFound()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);

        var response = await q.Client.GetAsync($"{q.QualificationUrl}/ties/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
