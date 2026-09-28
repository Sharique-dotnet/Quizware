using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 9b: start (question reservation), pause/resume, end,
/// abandon, live state, and the event timeline.</summary>
public class LiveMatchLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveMatchLifecycleTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Start_ReservesEverySegmentsQuestions_WithoutOverlap_AndOpensScoreRows()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 2, 2);

        var response = await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var started = (await response.Content.ReadFromJsonAsync<StartMatchResponse>())!;
        started.State.Should().Be("InProgress");
        started.QuestionsReserved.Should().Be(4);
        started.Participants.Should().HaveCount(3);
        started.Segments.Should().HaveCount(2);
        started.BuzzerAvailable.Should().BeFalse();

        var reservations = await h.ReadDbAsync(db => db.MatchQuestions.Where(q => q.MatchId == match.Id).ToListAsync());
        reservations.Should().HaveCount(4);
        reservations.Select(q => q.QuestionId).Should().OnlyHaveUniqueItems();
        reservations.Should().OnlyContain(q => q.State == MatchQuestionState.Reserved);
        (await h.ReadDbAsync(db => db.TeamMatchScores.CountAsync(s => s.MatchId == match.Id))).Should().Be(3);
    }

    [Fact]
    public async Task Start_NotEnoughQuestions_Returns409_AndLeavesMatchUnstarted()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedMcqAsync(2);
        var stage = await h.CreateStageAsync(("Mcq", 5));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));

        var response = await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("QUESTION_POOL_EXHAUSTED");
        (await h.Client.GetFromJsonAsync<MatchDetailResponse>($"{h.MatchesUrl}/{match.Id}"))!.State.Should().Be("Draft");
        (await h.ReadDbAsync(db => db.MatchQuestions.CountAsync(q => q.MatchId == match.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Start_WithOneTeam_ReturnsInsufficientParticipants()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(teamCount: 1);

        var response = await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("INSUFFICIENT_PARTICIPANTS");
    }

    [Fact]
    public async Task Start_Twice_ReturnsConflict_AndSetupIsFrozen()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();
        await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/start", null);

        var again = await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/start", null);
        var addTeam = await h.Client.PostAsJsonAsync(
            $"{h.MatchesUrl}/{match.Id}/participants", new AddMatchParticipantRequest(await h.CreateTeamAsync("Late"), 9));
        var delete = await h.Client.DeleteAsync($"{h.MatchesUrl}/{match.Id}");

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        addTeam.StatusCode.Should().Be(HttpStatusCode.Conflict);
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PauseAndResume_RoundTrip_AndPausingTwiceConflicts()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();
        var live = MatchTestHarness.LiveUrl(match.Id);
        await h.Client.PostAsync($"{live}/start", null);

        var paused = (await (await h.Client.PostAsync($"{live}/pause", null)).Content.ReadFromJsonAsync<LiveMatchStateResponse>())!;
        var pauseAgain = await h.Client.PostAsync($"{live}/pause", null);
        var resumed = (await (await h.Client.PostAsync($"{live}/resume", null)).Content.ReadFromJsonAsync<LiveMatchStateResponse>())!;

        paused.IsPaused.Should().BeTrue();
        paused.State.Should().Be("Paused");
        pauseAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        resumed.IsPaused.Should().BeFalse();
        resumed.State.Should().Be("InProgress");
    }

    [Fact]
    public async Task State_AfterStart_ShowsScoresProgressAndBuzzer()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 2, 3);
        await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/start", null);

        var state = (await h.Client.GetFromJsonAsync<LiveMatchStateResponse>($"{MatchTestHarness.LiveUrl(match.Id)}/state"))!;

        state.MatchName.Should().Be("Match 1");
        state.State.Should().Be("InProgress");
        state.CurrentSegment.Should().BeNull();
        state.CurrentQuestion.Should().BeNull();
        state.Participants.Should().HaveCount(3).And.OnlyContain(p => p.Score == 0 && p.Status == "Active");
        state.Progress.Should().Be(new MatchProgressDto(0, 5, 0, 2));
        state.Buzzer.Available.Should().BeFalse();
        state.CanUndo.Should().BeFalse();
    }

    [Fact]
    public async Task State_IsVisibleToDisplay_ButDisplayCannotStart()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();
        var display = await h.CreateClientAsync(Roles.Display);

        var state = await display.GetAsync($"{MatchTestHarness.LiveUrl(match.Id)}/state");
        var start = await display.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/start", null);

        state.StatusCode.Should().Be(HttpStatusCode.OK);
        start.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task State_OfAnotherProgramsMatch_IsNotFound()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();
        var other = await MatchTestHarness.CreateAsync(_factory);

        var response = await other.Client.GetAsync($"{MatchTestHarness.LiveUrl(match.Id)}/state");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task End_WithAllScoresLevel_CompletesTied_RanksEveryoneFirst_AndReleasesUnservedQuestions()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(3, 3);
        var live = MatchTestHarness.LiveUrl(match.Id);
        await h.Client.PostAsync($"{live}/start", null);

        var response = await h.Client.PostAsync($"{live}/end", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!.State.Should().Be("Completed");
        var stored = await h.ReadDbAsync(db => db.Matches.SingleAsync(m => m.Id == match.Id));
        stored.IsTied.Should().BeTrue();
        stored.WinnerTeamId.Should().BeNull();
        var participants = await h.ReadDbAsync(db => db.MatchParticipants.Where(p => p.MatchId == match.Id).ToListAsync());
        participants.Should().OnlyContain(p => p.FinalRank == 1);
        var questions = await h.ReadDbAsync(db => db.MatchQuestions.Where(q => q.MatchId == match.Id).ToListAsync());
        questions.Should().OnlyContain(q => q.State == MatchQuestionState.Released);
        var segments = await h.ReadDbAsync(db => db.MatchSegments.Where(s => s.MatchId == match.Id).ToListAsync());
        segments.Should().OnlyContain(s => s.State == MatchSegmentState.Skipped);
    }

    [Fact]
    public async Task End_BeforeStart_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();

        var response = await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(match.Id)}/end", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Abandon_RequiresReason_ThenReleasesQuestions_AndCannotBeRepeated()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();
        var live = MatchTestHarness.LiveUrl(match.Id);
        await h.Client.PostAsync($"{live}/start", null);

        var noReason = await h.Client.PostAsJsonAsync($"{live}/abandon", new AbandonMatchRequest(""));
        var abandoned = await h.Client.PostAsJsonAsync($"{live}/abandon", new AbandonMatchRequest("Power cut"));
        var again = await h.Client.PostAsJsonAsync($"{live}/abandon", new AbandonMatchRequest("Again"));

        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        abandoned.StatusCode.Should().Be(HttpStatusCode.OK);
        (await abandoned.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!.State.Should().Be("Abandoned");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var questions = await h.ReadDbAsync(db => db.MatchQuestions.Where(q => q.MatchId == match.Id).ToListAsync());
        questions.Should().OnlyContain(q => q.State == MatchQuestionState.Released);
    }

    [Fact]
    public async Task Abandoned_MatchesQuestions_CanBeDrawnByTheNextMatch()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var first = await h.CreateReadyToStartMatchAsync(2, 3);
        await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(first.Id)}/start", null);
        await h.Client.PostAsJsonAsync($"{MatchTestHarness.LiveUrl(first.Id)}/abandon", new AbandonMatchRequest("Restart"));
        var second = await h.CreateMatchAsync(first.StageId, 2, await h.CreateTeamsAsync(2));

        var response = await h.Client.PostAsync($"{MatchTestHarness.LiveUrl(second.Id)}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Timeline_RecordsEachLifecycleEventInOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();
        var live = MatchTestHarness.LiveUrl(match.Id);
        await h.Client.PostAsync($"{live}/start", null);
        await h.Client.PostAsync($"{live}/pause", null);
        await h.Client.PostAsync($"{live}/resume", null);
        await h.Client.PostAsync($"{live}/end", null);

        var timeline = (await h.Client.GetFromJsonAsync<MatchTimelineResponse>($"{live}/timeline"))!;

        timeline.Events.Select(e => e.EventType).Should().Equal("MatchStarted", "MatchPaused", "MatchResumed", "MatchCompleted");
        timeline.Events.Select(e => e.SequenceNumber).Should().Equal(1, 2, 3, 4);
    }
}
