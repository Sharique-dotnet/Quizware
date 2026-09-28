using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Api.IntegrationTests;

/// <summary>P9-08: pass direction by seat, the question's own pass limit, and
/// reveal-when-every-team-passes.</summary>
public class LivePassingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LivePassingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<MatchTestHarness.StartedMatch> PassingMatchAsync(
        MatchTestHarness h, int teams, int maxPassCount, PassDirection direction, bool revealIfAllPass = true,
        bool segmentAllowsPassing = false, int? segmentMaxPassCount = null)
    {
        await h.ResetScoringDefaultsAsync();
        await h.SeedAsync((db, p) =>
        {
            var q = PassingQuestion.Create(p, QuestionOwnerScope.Program, "Pass me", DifficultyLevel.Medium, "en", "t",
                maxPassCount: maxPassCount, passDirection: direction, revealAnswerIfAllPass: revealIfAllPass);
            q.Approve(Guid.NewGuid());
            db.Questions.Add(q);
            db.QuestionOptions.Add(QuestionOption.Create(q.Id, "Right", true, 0, "t"));
            db.QuestionOptions.Add(QuestionOption.Create(q.Id, "Wrong", false, 1, "t"));
        });
        var stage = await h.CreateStageAsync(("Passing", 1));
        if (segmentAllowsPassing)
        {
            await h.ConfigureTemplatesAsync(stage.Id, allowPassing: true, maxPassCount: segmentMaxPassCount);
        }

        return await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(teams)));
    }

    private static async Task<HttpResponseMessage> PassAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started, Guid questionId) =>
        await h.Client.PostAsJsonAsync(
            $"{started.Live}/pass", new PassQuestionRequest(questionId, (await h.StateAsync(started)).ActiveParticipant!.ParticipantId));

    private static async Task<int> HolderSeatAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started) =>
        (await h.StateAsync(started)).ActiveParticipant!.SeatNumber;

    [Fact]
    public async Task APassingQuestion_CarriesItsOwnRules_EvenWhereTheSegmentDoesNotAllowPassing()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await PassingMatchAsync(h, teams: 3, maxPassCount: 2, PassDirection.Clockwise);
        var question = await h.ServeAsync(started);
        var seats = new List<int> { await HolderSeatAsync(h, started) };

        (await PassAsync(h, started, question.MatchQuestionId)).StatusCode.Should().Be(HttpStatusCode.OK);
        seats.Add(await HolderSeatAsync(h, started));
        (await PassAsync(h, started, question.MatchQuestionId)).StatusCode.Should().Be(HttpStatusCode.OK);
        seats.Add(await HolderSeatAsync(h, started));
        var third = await PassAsync(h, started, question.MatchQuestionId);

        seats.Should().Equal(1, 2, 3);
        third.StatusCode.Should().Be(HttpStatusCode.Conflict, "the question allows 2 passes");
    }

    [Fact]
    public async Task Anticlockwise_PassesToTheNextSeatDown()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await PassingMatchAsync(h, teams: 3, maxPassCount: 2, PassDirection.Anticlockwise);
        var question = await h.ServeAsync(started);

        await PassAsync(h, started, question.MatchQuestionId);

        (await HolderSeatAsync(h, started)).Should().Be(3);
    }

    [Fact]
    public async Task TheLowerOfTheQuestionAndSegmentLimits_Applies()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await PassingMatchAsync(
            h, teams: 3, maxPassCount: 2, PassDirection.Clockwise, segmentAllowsPassing: true, segmentMaxPassCount: 1);
        var question = await h.ServeAsync(started);

        var first = await PassAsync(h, started, question.MatchQuestionId);
        var second = await PassAsync(h, started, question.MatchQuestionId);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task WhenEveryTeamPasses_TheAnswerIsRevealed_AndTheQuestionClosesUnanswered()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await PassingMatchAsync(h, teams: 2, maxPassCount: 5, PassDirection.Clockwise, revealIfAllPass: true);
        var question = await h.ServeAsync(started);

        await PassAsync(h, started, question.MatchQuestionId);
        var last = await PassAsync(h, started, question.MatchQuestionId);

        last.StatusCode.Should().Be(HttpStatusCode.OK);
        (await last.Content.ReadFromJsonAsync<LiveMatchStateResponse>())!.CurrentQuestion.Should().BeNull();
        var stored = await h.ReadDbAsync(db => db.MatchQuestions.SingleAsync(q => q.Id == question.MatchQuestionId));
        stored.State.Should().Be(MatchQuestionState.Skipped);
        stored.RevealedAtUtc.Should().NotBeNull();
        (await h.ReadDbAsync(db => db.AnswerRecords.CountAsync(a => a.MatchQuestionId == question.MatchQuestionId && a.Outcome == AnswerOutcome.Passed)))
            .Should().Be(2);
    }

    [Fact]
    public async Task WhenEveryTeamPasses_WithoutRevealIfAllPass_TheLastPassIsRefused()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await PassingMatchAsync(h, teams: 2, maxPassCount: 5, PassDirection.Clockwise, revealIfAllPass: false);
        var question = await h.ServeAsync(started);

        await PassAsync(h, started, question.MatchQuestionId);
        var last = await PassAsync(h, started, question.MatchQuestionId);

        last.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
