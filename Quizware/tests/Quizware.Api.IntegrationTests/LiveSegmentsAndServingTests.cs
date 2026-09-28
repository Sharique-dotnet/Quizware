using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Contracts.V1.Matches;
using Quizware.Api.Contracts.V1.Stages;
using Quizware.Application.Authorization;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 9c: opening/closing/skipping segments, serving questions to
/// the team whose turn it is, reveal, skip, and team topic picks.</summary>
public class LiveSegmentsAndServingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveSegmentsAndServingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenSegment_FixedOrder_OnlyTheNextSegmentMayOpen()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(2, 2, 2);
        var live = MatchTestHarness.LiveUrl(match.Id);
        await h.Client.PostAsync($"{live}/start", null);
        var segments = (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{h.MatchesUrl}/{match.Id}/segments"))!.Segments;

        var options = (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{live}/segments/next-options"))!;
        var outOfOrder = await h.Client.PostAsync($"{live}/segments/{segments[1].Id}/open", null);
        var inOrder = await h.Client.PostAsync($"{live}/segments/{segments[0].Id}/open", null);
        var optionsWhileOpen = (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{live}/segments/next-options"))!;

        options.Segments.Should().ContainSingle().Which.Id.Should().Be(segments[0].Id);
        outOfOrder.StatusCode.Should().Be(HttpStatusCode.Conflict);
        inOrder.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = (await inOrder.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!;
        state.CurrentSegment!.SegmentId.Should().Be(segments[0].Id);
        state.CurrentSegment.PlannedQuestionCount.Should().Be(2);
        optionsWhileOpen.Segments.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenSegment_OperatorChoice_AnyPendingSegmentMayOpen()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(2, 2, 2);
        await h.Client.PutAsJsonAsync(
            $"/api/v1/programs/{h.ProgramId}/stages/{match.StageId}/segment-order-mode", new SetSegmentOrderModeRequest("OperatorChoice"));
        var live = MatchTestHarness.LiveUrl(match.Id);
        await h.Client.PostAsync($"{live}/start", null);
        var segments = (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{h.MatchesUrl}/{match.Id}/segments"))!.Segments;

        var options = (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{live}/segments/next-options"))!;
        var second = await h.Client.PostAsync($"{live}/segments/{segments[1].Id}/open", null);

        options.Segments.Should().HaveCount(2);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenSegment_WhilePaused_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync();
        var live = MatchTestHarness.LiveUrl(match.Id);
        await h.Client.PostAsync($"{live}/start", null);
        await h.Client.PostAsync($"{live}/pause", null);
        var segments = (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{h.MatchesUrl}/{match.Id}/segments"))!.Segments;

        var response = await h.Client.PostAsync($"{live}/segments/{segments[0].Id}/open", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Serve_RotatesTurnsAcrossActiveTeams()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 4));
        var turns = new List<int>();

        for (var i = 0; i < 4; i++)
        {
            var question = await h.ServeAsync(started);
            turns.Add((await h.StateAsync(started)).ActiveParticipant!.TurnOrder);
            await h.Client.PostAsJsonAsync($"{started.Live}/questions/{question.MatchQuestionId}/skip", new SkipQuestionRequest("next"));
        }

        turns.Should().Equal(1, 2, 3, 1);
    }

    [Fact]
    public async Task Serve_ReturnsTheQuestion_StartsItsTimer_AndRecordsUsage()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var match = await h.CreateReadyToStartMatchAsync(2, 2);
        await h.ConfigureTemplatesAsync(match.StageId, timeLimitSeconds: 30);
        var started = await h.StartWithFirstSegmentOpenAsync(match);

        var question = await h.ServeAsync(started);

        question.State.Should().Be("Active");
        question.Format.Should().Be("Mcq");
        question.Options.Should().HaveCount(2);
        question.CorrectOptionId.Should().NotBeNull("an operator may see the answer before it is revealed");
        question.TimeLimitSeconds.Should().Be(30);
        question.RemainingSeconds.Should().BeInRange(0, 30);
        var mq = await h.ReadDbAsync(db => db.MatchQuestions.SingleAsync(q => q.Id == question.MatchQuestionId));
        (await h.ReadDbAsync(db => db.QuestionUsageHistories.CountAsync(u => u.MatchId == match.Id && u.QuestionId == mq.QuestionId)))
            .Should().Be(1);
        (await h.ReadDbAsync(db => db.Questions.SingleAsync(q => q.Id == mq.QuestionId))).TimesUsed.Should().Be(1);
        (await h.StateAsync(started)).CurrentSegment!.ServedQuestionCount.Should().Be(1);
    }

    [Fact]
    public async Task Serve_WhileAQuestionIsOnScreen_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await h.ServeAsync(started);

        var again = await h.Client.PostAsJsonAsync($"{started.Live}/questions/serve", new ServeQuestionRequest(started.Segments[0].Id));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Serve_WhenSegmentIsExhausted_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 1));
        var only = await h.ServeAsync(started);
        await h.Client.PostAsJsonAsync($"{started.Live}/questions/{only.MatchQuestionId}/skip", new SkipQuestionRequest("x"));

        var response = await h.Client.PostAsJsonAsync($"{started.Live}/questions/serve", new ServeQuestionRequest(started.Segments[0].Id));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DisplayClient_SeesTheAnswerOnlyAfterReveal()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var display = await h.CreateClientAsync(Roles.Display);
        var served = await h.ServeAsync(started);

        var before = (await display.GetFromJsonAsync<LiveMatchStateResponse>($"{started.Live}/state"))!;
        var reveal = await h.Client.PostAsync($"{started.Live}/questions/{served.MatchQuestionId}/reveal", null);
        var after = (await display.GetFromJsonAsync<LiveMatchStateResponse>($"{started.Live}/state"))!;

        before.CurrentQuestion!.CorrectOptionId.Should().BeNull();
        reveal.StatusCode.Should().Be(HttpStatusCode.OK);
        after.CurrentQuestion!.CorrectOptionId.Should().Be(served.CorrectOptionId);
    }

    [Fact]
    public async Task NextQuestion_PreviewsWithoutServing_AndAnUnservedQuestionCannotBeRevealed()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));

        var preview = (await h.Client.GetFromJsonAsync<CurrentQuestionDto>($"{started.Live}/next-question"))!;
        var reveal = await h.Client.PostAsync($"{started.Live}/questions/{preview.MatchQuestionId}/reveal", null);
        var served = await h.ServeAsync(started);

        preview.State.Should().Be("Reserved");
        reveal.StatusCode.Should().Be(HttpStatusCode.Conflict);
        served.MatchQuestionId.Should().Be(preview.MatchQuestionId);
    }

    [Fact]
    public async Task SkipQuestion_RequiresReason_AndClearsTheScreen()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var served = await h.ServeAsync(started);

        var noReason = await h.Client.PostAsJsonAsync($"{started.Live}/questions/{served.MatchQuestionId}/skip", new SkipQuestionRequest(""));
        var skipped = await h.Client.PostAsJsonAsync($"{started.Live}/questions/{served.MatchQuestionId}/skip", new SkipQuestionRequest("Typo"));

        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        skipped.StatusCode.Should().Be(HttpStatusCode.OK);
        (await skipped.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!.CurrentQuestion.Should().BeNull();
    }

    [Fact]
    public async Task CloseSegment_RefusesWhileAQuestionIsOnScreen_ThenReleasesUnservedQuestions()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 3, 2));
        var served = await h.ServeAsync(started);
        var closeUrl = $"{started.Live}/segments/{started.Segments[0].Id}/close";

        var whileActive = await h.Client.PostAsync(closeUrl, null);
        await h.Client.PostAsJsonAsync($"{started.Live}/questions/{served.MatchQuestionId}/skip", new SkipQuestionRequest("x"));
        var closed = await h.Client.PostAsync(closeUrl, null);

        whileActive.StatusCode.Should().Be(HttpStatusCode.Conflict);
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = (await closed.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!;
        state.CurrentSegment.Should().BeNull();
        state.Progress.SegmentsCompleted.Should().Be(1);
        var released = await h.ReadDbAsync(db => db.MatchQuestions.CountAsync(
            q => q.MatchSegmentId == started.Segments[0].Id && q.State == MatchQuestionState.Released));
        released.Should().Be(2);
        var options = (await h.Client.GetFromJsonAsync<MatchSegmentsResponse>($"{started.Live}/segments/next-options"))!;
        options.Segments.Should().ContainSingle().Which.Id.Should().Be(started.Segments[1].Id);
    }

    [Fact]
    public async Task SkipSegment_PendingSegment_IsSkipped_AndNeedsAReason()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 1, 1));
        var skipUrl = $"{started.Live}/segments/{started.Segments[1].Id}/skip";

        var noReason = await h.Client.PostAsJsonAsync(skipUrl, new SkipSegmentRequest(" "));
        var skipped = await h.Client.PostAsJsonAsync(skipUrl, new SkipSegmentRequest("Running late"));

        noReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        skipped.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = (await skipped.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!;
        state.Progress.QuestionsTotal.Should().Be(1, "a skipped segment's questions no longer count");
    }

    [Fact]
    public async Task LiveReorder_WhenStageForbidsIt_ReturnsSegmentNotReorderable()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 1, 1));

        var response = await h.Client.PutAsJsonAsync(
            $"{started.Live}/segments/reorder",
            new ReorderMatchSegmentsRequest(started.Segments.Select(s => s.Id).Reverse().ToList(), "Swap"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MatchTestHarness.ErrorCodeAsync(response)).Should().Be("SEGMENT_NOT_REORDERABLE");
    }

    [Fact]
    public async Task TopicPicks_OnlyTheTeamOnTurnMayPick_AndThePickIsServedNext()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        var history = await h.SeedTopicAsync("History");
        var science = await h.SeedTopicAsync("Science");
        await h.SeedMcqAsync(2, history);
        await h.SeedMcqAsync(2, science);
        var stage = await h.CreateStageAsync(("Mcq", 4));
        await h.ConfigureTemplatesAsync(stage.Id, topicMode: TopicSelectionMode.TeamPicksTopic, topicChoiceLimit: 2);
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));
        var started = await h.StartWithFirstSegmentOpenAsync(match);
        var state = await h.StateAsync(started);
        var onTurn = state.ActiveParticipant!.ParticipantId;
        var offTurn = state.Participants.Single(p => p.ParticipantId != onTurn).ParticipantId;
        var nextBefore = (await h.Client.GetFromJsonAsync<CurrentQuestionDto>($"{started.Live}/next-question"))!;
        var pick = nextBefore.TopicName == "History" ? "Science" : "History";

        var available = (await h.Client.GetFromJsonAsync<AvailableTopicsResponse>($"{started.Live}/topics/available"))!;
        var wrongTeam = await h.Client.PostAsJsonAsync($"{started.Live}/topics/select", new SelectTopicRequest(offTurn, pick));
        var unknown = await h.Client.PostAsJsonAsync($"{started.Live}/topics/select", new SelectTopicRequest(onTurn, "Geography"));
        var picked = await h.Client.PostAsJsonAsync($"{started.Live}/topics/select", new SelectTopicRequest(onTurn, pick));
        var served = await h.ServeAsync(started);

        available.Topics.Should().Equal("History", "Science");
        available.TopicChoiceLimit.Should().Be(2);
        wrongTeam.StatusCode.Should().Be(HttpStatusCode.Conflict);
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        picked.StatusCode.Should().Be(HttpStatusCode.OK);
        served.TopicName.Should().Be(pick);
    }

    [Fact]
    public async Task TopicSelect_InASegmentWithoutTopicPicks_ReturnsConflict()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var onTurn = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;

        var available = (await h.Client.GetFromJsonAsync<AvailableTopicsResponse>($"{started.Live}/topics/available"))!;
        var select = await h.Client.PostAsJsonAsync($"{started.Live}/topics/select", new SelectTopicRequest(onTurn, "History"));

        available.Topics.Should().BeEmpty();
        select.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Timeline_RecordsSegmentAndQuestionEvents()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 1));
        var served = await h.ServeAsync(started);
        await h.Client.PostAsync($"{started.Live}/questions/{served.MatchQuestionId}/reveal", null);
        await h.Client.PostAsJsonAsync($"{started.Live}/questions/{served.MatchQuestionId}/skip", new SkipQuestionRequest("x"));
        await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[0].Id}/close", null);

        var timeline = (await h.Client.GetFromJsonAsync<MatchTimelineResponse>($"{started.Live}/timeline"))!;

        timeline.Events.Select(e => e.EventType).Should().Equal(
            "MatchStarted", "SegmentOpened", "QuestionServed", "QuestionRevealed", "QuestionSkipped", "SegmentCompleted");
    }

    [Fact]
    public async Task EachSegment_StartsTheRotationAgain_WithTheFirstTeam()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 2, 2));
        var firstSegmentTurns = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            var question = await h.ServeAsync(started);
            firstSegmentTurns.Add((await h.StateAsync(started)).ActiveParticipant!.TurnOrder);
            await h.Client.PostAsJsonAsync($"{started.Live}/questions/{question.MatchQuestionId}/skip", new SkipQuestionRequest("next"));
        }

        await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[0].Id}/close", null);
        await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[1].Id}/open", null);
        await h.ServeAsync(started, 1);

        firstSegmentTurns.Should().Equal(1, 2);
        (await h.StateAsync(started)).ActiveParticipant!.TurnOrder.Should().Be(1, "BR-2.2: the index is the question's position in its segment");
    }

    [Fact]
    public async Task RapidFire_NobodyHoldsTheQuestion_SoAnyTeamMayAnswer()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedAsync((db, p) =>
        {
            var q = Domain.QuestionBank.RapidFireQuestion.CreateStored(
                p, Domain.Enums.QuestionOwnerScope.Program, "Capital of Peru?", "Lima", Domain.Enums.DifficultyLevel.Medium, "en", "t");
            q.Approve(Guid.NewGuid());
            db.Questions.Add(q);
        });
        var stage = await h.CreateStageAsync(("RapidFire", 1));
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(3)));
        var question = await h.ServeAsync(started);
        var state = await h.StateAsync(started);
        var lastInTurn = state.Participants.Single(p => p.TurnOrder == 3).ParticipantId;

        var answer = await h.AnswerAsync(started, question.MatchQuestionId, lastInTurn, "Correct");

        state.ActiveParticipant.Should().BeNull();
        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        (await answer.Content.ReadFromJsonAsync<RecordAnswerResponse>())!.PointsAwarded.Should().Be(5);
    }
}
