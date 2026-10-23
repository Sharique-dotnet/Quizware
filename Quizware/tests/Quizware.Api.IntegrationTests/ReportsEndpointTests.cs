using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quizware.Api.Contracts.V1.Reports;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 13: the match report (P13-01), stage summary (P13-02),
/// team performance (P13-03) and question usage (P13-04) reports.</summary>
public class ReportsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string ReportsUrl(QualificationTestHarness q) => $"/api/v1/programs/{q.Match.ProgramId}/reports";

    private static async Task<T> GetOkAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task MatchReport_HasEachTeamsLine_EveryServedQuestion_AndTheFullTimeline()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(6);
        var t = await q.Match.CreateTeamsAsync(3);
        var match = await q.PlayMatchAsync(stage.Id, (t[0], "CC"), (t[1], "CI"), (t[2], "II"));

        var report = await GetOkAsync<MatchReportResponse>(q.Client, $"{ReportsUrl(q)}/matches/{match.Id}");

        report.MatchId.Should().Be(match.Id);
        report.State.Should().Be("Completed");
        report.StageName.Should().NotBeNullOrEmpty();
        report.WinnerTeamId.Should().Be(t[0]);
        report.Teams.Select(x => (x.TeamId, x.Score, x.Rank)).Should().Equal((t[0], 20, 1), (t[1], 10, 2), (t[2], 0, 3));
        report.Teams[1].CorrectCount.Should().Be(1);
        report.Teams[1].IncorrectCount.Should().Be(1);
        report.Questions.Should().HaveCount(6);
        report.Questions.Should().OnlyContain(x => x.Format == "Mcq" && x.QuestionText != null && x.Answers.Count == 1);
        report.Questions.SelectMany(x => x.Answers).Count(a => a.IsCorrect == true).Should().Be(3);
        report.Events.Select(e => e.SequenceNumber).Should().BeInAscendingOrder();
        report.Events.Select(e => e.EventType).Should().Contain(["MatchStarted", "QuestionServed", "AnswerRecorded"]);
        report.Timeline.Should().HaveSameCount(report.Events);
        report.Timeline[0].Should().StartWith("#1 ").And.EndWith(report.Events[0].EventType);
    }

    [Fact]
    public async Task StageSummary_ShowsMatchesStandingsAndQualifiers_WithTieBreakProvenance()
    {
        // Team 2 and team 5 finish level on 10 for the one best-remaining slot;
        // team 5 answered fewer incorrectly, so the criteria settle it.
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(9);
        await q.CreateStageAsync(3);
        await q.SetRuleAsync(stage.Id, winnersPerMatch: 1, bestRemaining: 1);
        await q.SetTieBreakRuleAsync(stage.Id);
        var t = await q.Match.CreateTeamsAsync(6);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "CII"), (t[2], "III"));
        await q.PlayMatchAsync(stage.Id, (t[3], "CCC"), (t[4], "CIS"), (t[5], "III"));
        (await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/ties/detect", null)).EnsureSuccessStatusCode();
        var commit = await q.Client.PostAsync($"{q.QualificationUrl}/stages/{stage.Id}/commit", null);
        commit.StatusCode.Should().Be(HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());

        var report = await GetOkAsync<StageSummaryReportResponse>(q.Client, $"{ReportsUrl(q)}/stages/{stage.Id}");

        report.StageId.Should().Be(stage.Id);
        report.Matches.Should().HaveCount(2).And.OnlyContain(m => m.State == "Completed");
        report.Matches.Select(m => m.WinnerTeamId).Should().BeEquivalentTo(new Guid?[] { t[0], t[3] });
        report.Standings.Select(s => s.TeamId).Take(2).Should().BeEquivalentTo([t[0], t[3]]);
        report.Qualifiers.Select(x => (x.TeamId, x.Reason, x.IsCommitted)).Should().BeEquivalentTo(new[]
        {
            (t[0], "MatchWinner", true), (t[3], "MatchWinner", true), (t[4], "CriteriaTieBreak", true),
        });
        var viaTie = report.Qualifiers.Single(x => x.TeamId == t[4]);
        viaTie.TieBreakEventId.Should().NotBeNull();
        viaTie.TieBreakProvenance.Should().Contain("criteria (FewerIncorrect)");
        var tie = report.TieBreaks.Single(x => x.TieBreakEventId == viaTie.TieBreakEventId);
        tie.ResolutionMethod.Should().Be("Criteria");
        tie.Participants.Select(p => p.TeamId).Should().BeEquivalentTo([t[1], t[4]]);
        tie.Participants.First().TeamId.Should().Be(t[4]);
        report.QualificationRecord.Should().HaveCount(3);
        report.QualificationRecord.Should().Contain(line => line.Contains("CriteriaTieBreak") && line.Contains("criteria (FewerIncorrect)"));
    }

    [Fact]
    public async Task TeamPerformance_TotalsCompletedMatches_WithAccuracyAndAPerFormatBreakdown()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(4);
        var t = await q.Match.CreateTeamsAsync(3);
        var won = await q.PlayMatchAsync(stage.Id, (t[0], "CC"), (t[1], "CI"));
        var lost = await q.PlayMatchAsync(stage.Id, (t[0], "CI"), (t[2], "CC"));

        var report = await GetOkAsync<TeamPerformanceReportResponse>(q.Client, $"{ReportsUrl(q)}/teams/{t[0]}");

        report.TeamId.Should().Be(t[0]);
        report.MatchesPlayed.Should().Be(2);
        report.TotalScore.Should().Be(30);
        report.AverageScore.Should().Be(15);
        report.Wins.Should().Be(1);
        report.CorrectCount.Should().Be(3);
        report.IncorrectCount.Should().Be(1);
        report.Accuracy.Should().Be(0.75);
        report.OverallRank.Should().BeGreaterThan(0);
        report.Matches.Select(m => (m.MatchId, m.Score, m.Won)).Should().BeEquivalentTo(new[] { (won.Id, 20, true), (lost.Id, 10, false) });
        report.Formats.Should().ContainSingle().Which.Should().Be(new TeamFormatPerformance("Mcq", 4, 3, 30));
    }

    [Fact]
    public async Task QuestionUsage_FlagsQuestionsEveryoneGotRight_AndNobodyGotRight()
    {
        var q = await QualificationTestHarness.CreateAsync(_factory);
        var stage = await q.CreateStageAsync(6);
        var otherStage = await q.CreateStageAsync(2);
        var t = await q.Match.CreateTeamsAsync(2);
        await q.PlayMatchAsync(stage.Id, (t[0], "CCC"), (t[1], "IIS"));
        var url = $"{ReportsUrl(q)}/questions/usage";

        var all = await GetOkAsync<QuestionUsageReportResponse>(q.Client, url);
        var forOtherStage = await GetOkAsync<QuestionUsageReportResponse>(q.Client, $"{url}?stageId={otherStage.Id}");
        var flagged = await GetOkAsync<QuestionUsageReportResponse>(q.Client, $"{url}?stageId={stage.Id}&flaggedOnly=true");

        all.TotalQuestions.Should().Be(6);
        all.Entries.Should().OnlyContain(e => e.TimesUsed == 1 && e.MatchesUsed == 1 && e.Format == "Mcq");
        all.Entries.Count(e => e.EveryoneCorrect).Should().Be(3);
        all.Entries.Where(e => e.EveryoneCorrect).Should().OnlyContain(e => e.CorrectRate == 1 && e.Flags.Contains("EveryoneCorrect"));
        all.Entries.Count(e => e.NobodyCorrect).Should().Be(2);
        all.Entries.Should().ContainSingle(e => e.Attempts == 0).Which.Flags.Should().Equal("NeverAnswered");
        all.FlaggedCount.Should().Be(6);
        forOtherStage.Entries.Should().BeEmpty();
        flagged.Entries.Should().HaveCount(flagged.FlaggedCount);
    }

    [Fact]
    public async Task Reports_AreScopedToTheProgram_AndClosedToOperators()
    {
        var mine = await QualificationTestHarness.CreateAsync(_factory);
        var theirs = await QualificationTestHarness.CreateAsync(_factory);
        var theirMatch = await theirs.Match.CreateReadyToStartMatchAsync(2, 2);
        var operatorClient = await mine.Match.CreateClientAsync(Roles.Operator);
        var myMatch = await mine.Match.CreateReadyToStartMatchAsync(2, 2);

        var otherMatch = await mine.Client.GetAsync($"{ReportsUrl(mine)}/matches/{theirMatch.Id}");
        var otherStage = await mine.Client.GetAsync($"{ReportsUrl(mine)}/stages/{theirMatch.StageId}");
        var asOperator = await operatorClient.GetAsync($"{ReportsUrl(mine)}/matches/{myMatch.Id}");
        var otherTeam = await mine.Client.GetAsync($"{ReportsUrl(mine)}/teams/{(await theirs.Match.CreateTeamAsync("Theirs"))}");
        var otherStageUsage = await mine.Client.GetAsync($"{ReportsUrl(mine)}/questions/usage?stageId={theirMatch.StageId}");

        otherMatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherStage.StatusCode.Should().Be(HttpStatusCode.NotFound);
        asOperator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        otherTeam.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherStageUsage.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
