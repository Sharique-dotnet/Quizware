using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Standings;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>The integration tests docs/Implementation-Plan.md lists as
/// required for Phase 9: a full match, disqualification mid-segment, restart
/// mid-question, and a stage without the Passing format. (Double submission
/// and the ProgramAdmin-only authorisation are covered in
/// LiveAnswersAndScoringTests.)</summary>
public class Phase9RequiredTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public Phase9RequiredTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Serves every question in the open segment, answering each with
    /// the next outcome from <paramref name="outcomes"/>; returns who held each.</summary>
    private static async Task<List<Guid>> PlaySegmentAsync(
        MatchTestHarness h, MatchTestHarness.StartedMatch started, int segmentIndex, params string[] outcomes)
    {
        var holders = new List<Guid>();
        foreach (var outcome in outcomes)
        {
            var question = await h.ServeAsync(started, segmentIndex);
            var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
            holders.Add(holder);
            (await h.AnswerAsync(started, question.MatchQuestionId, holder, outcome)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[segmentIndex].Id}/close", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return holders;
    }

    [Fact]
    public async Task AFullThreeTeamMatch_RunsStartToFinish_WithCorrectFinalStandings()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 3, 3);
        (await (await h.Client.PostAsync($"{h.MatchesUrl}/{match.Id}/ready", null)).Content.ReadFromJsonAsync<MatchReadyResponse>())!
            .IsReady.Should().BeTrue();
        var started = await h.StartWithFirstSegmentOpenAsync(match);

        // Turn order 1,2,3 in each segment. Team 1: 2 correct, team 2: 1, team 3: 0.
        var first = await PlaySegmentAsync(h, started, 0, "Correct", "Correct", "Incorrect");
        (await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[1].Id}/open", null)).EnsureSuccessStatusCode();
        await PlaySegmentAsync(h, started, 1, "Correct", "Incorrect", "Incorrect");
        var end = await h.Client.PostAsync($"{started.Live}/end", null);

        end.StatusCode.Should().Be(HttpStatusCode.OK);
        var teamOf = (await h.ReadDbAsync(db => db.MatchParticipants.Where(p => p.MatchId == match.Id).ToListAsync()))
            .ToDictionary(p => p.Id);
        var (t1, t2, t3) = (teamOf[first[0]].TeamId, teamOf[first[1]].TeamId, teamOf[first[2]].TeamId);
        var stored = await h.ReadDbAsync(db => db.Matches.SingleAsync(m => m.Id == match.Id));
        stored.State.Should().Be(MatchState.Completed);
        stored.WinnerTeamId.Should().Be(t1);
        teamOf.Values.Select(p => (p.TeamId, p.FinalScore, p.FinalRank)).Should().BeEquivalentTo(new[] { (t1, 20, (int?)1), (t2, 10, (int?)2), (t3, 0, (int?)3) });

        var standings = (await h.Client.GetFromJsonAsync<StageStandingsResponse>(
            $"/api/v1/programs/{h.ProgramId}/standings/stages/{match.StageId}"))!.Standings;
        standings.Select(s => (s.TeamId, s.Score, s.Rank)).Should().Equal((t1, 20, 1), (t2, 10, 2), (t3, 0, 3));
    }

    [Fact]
    public async Task DisqualificationMidSegment_FinishesWithTwoTeams_WithNoFakeAnswers()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 6));
        var q1 = await h.ServeAsync(started);
        var removed = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, q1.MatchQuestionId, removed, "Correct");

        (await h.DisqualifyAsync(started, removed)).StatusCode.Should().Be(HttpStatusCode.OK);
        var holders = await PlaySegmentAsync(h, started, 0, "Correct", "Incorrect", "Correct", "Incorrect", "Correct");
        await h.Client.PostAsync($"{started.Live}/end", null);

        holders.Should().NotContain(removed);
        holders.Distinct().Should().HaveCount(2);
        var removedAnswers = await h.ReadDbAsync(db => db.AnswerRecords.CountAsync(a => a.MatchParticipantId == removed));
        removedAnswers.Should().Be(1, "only the answer it gave before removal — none were invented for it");
        var turnOrders = (await h.StateAsync(started)).Participants.Where(p => p.Status == "Active").Select(p => p.TurnOrder);
        turnOrders.Should().BeEquivalentTo([1, 2]);
        (await h.ReadDbAsync(db => db.Matches.SingleAsync(m => m.Id == started.Match.Id))).State.Should().Be(MatchState.Completed);
    }

    [Fact]
    public async Task RestartMidQuestion_ResumesAtTheSameQuestion_WithTheSameUpcomingOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 4));
        var onScreen = await h.ServeAsync(started);
        var upcoming = await h.ReadDbAsync(db => db.MatchQuestions
            .Where(q => q.MatchSegmentId == started.Segments[0].Id && q.State == MatchQuestionState.Reserved)
            .OrderBy(q => q.OrderIndex)
            .Select(q => q.Id)
            .ToListAsync());

        // Nothing about a live match is held in memory, so a brand-new client
        // (a restarted console against a restarted API) sees exactly what the
        // database says.
        var restarted = await h.CreateClientAsync(Application.Authorization.Roles.ProgramAdmin, Application.Authorization.Roles.Operator);
        var state = (await restarted.GetFromJsonAsync<LiveMatchStateResponse>($"{started.Live}/state"))!;
        var served = new List<Guid>();
        var current = state.CurrentQuestion!;
        for (var i = 0; i < upcoming.Count; i++)
        {
            var holder = (await restarted.GetFromJsonAsync<LiveMatchStateResponse>($"{started.Live}/state"))!.ActiveParticipant!.ParticipantId;
            await restarted.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
                current.MatchQuestionId, holder, "Incorrect", null, null, null, 0, "Operator", null, null));
            var next = await restarted.PostAsJsonAsync($"{started.Live}/questions/serve", new ServeQuestionRequest(started.Segments[0].Id));
            current = (await next.Content.ReadFromJsonAsync<CurrentQuestionDto>())!;
            served.Add(current.MatchQuestionId);
        }

        state.CurrentQuestion!.MatchQuestionId.Should().Be(onScreen.MatchQuestionId);
        state.CurrentQuestion.TimerStartedAtUtc.Should().Be(onScreen.TimerStartedAtUtc);
        served.Should().Equal(upcoming);
    }

    [Fact]
    public async Task AStageWithoutPassing_RunsACompleteMatch()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedMcqAsync(2);
        await h.SeedAsync((db, p) =>
        {
            for (var i = 0; i < 2; i++)
            {
                var card = Domain.QuestionBank.CardQuestion.Create(p, QuestionOwnerScope.Program, $"Card {i}", DifficultyLevel.Medium, "en", "t");
                card.Approve(Guid.NewGuid());
                db.Questions.Add(card);
                db.QuestionOptions.Add(Domain.QuestionBank.QuestionOption.Create(card.Id, "Ace", true, 0, "t"));
                db.QuestionOptions.Add(Domain.QuestionBank.QuestionOption.Create(card.Id, "King", false, 1, "t"));
            }
        });
        var stage = await h.CreateStageAsync(("Mcq", 2), ("Card", 2));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));
        (await h.ReadDbAsync(db => db.Questions.CountAsync(q => q.ProgramId == h.ProgramId && q.FormatCode == QuestionFormatCode.Passing)))
            .Should().Be(0);

        var ready = (await (await h.Client.PostAsync($"{h.MatchesUrl}/{match.Id}/ready", null)).Content.ReadFromJsonAsync<MatchReadyResponse>())!;
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        await PlaySegmentAsync(h, started, 0, "Correct", "Incorrect");
        (await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[1].Id}/open", null)).EnsureSuccessStatusCode();
        await PlaySegmentAsync(h, started, 1, "Correct", "Correct");
        var end = await h.Client.PostAsync($"{started.Live}/end", null);

        ready.IsReady.Should().BeTrue(string.Join("; ", ready.Blockers));
        end.StatusCode.Should().Be(HttpStatusCode.OK);
        (await end.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!.State.Should().Be("Completed");
    }
}
