using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Domain.Enums;

namespace Quizware.Api.IntegrationTests;

/// <summary>P9-15 / BR-5.6: a sudden-death segment closes as soon as one team
/// leads, once every team has faced the same number of questions.</summary>
public class LiveSuddenDeathTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveSuddenDeathTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<MatchTestHarness.StartedMatch> SuddenDeathMatchAsync(MatchTestHarness h, bool suddenDeath = true)
    {
        var match = await h.CreateReadyToStartMatchAsync(2, 6);
        if (suddenDeath)
        {
            await h.SeedAsync((db, _) => db.MatchSegments.Single(s => s.MatchId == match.Id).MakeSuddenDeath());
        }

        return await h.StartWithFirstSegmentOpenAsync(match);
    }

    private static async Task<RecordAnswerResponse> ServeAndAnswerAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started, string outcome)
    {
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        return (await (await h.AnswerAsync(started, question.MatchQuestionId, holder, outcome)).Content.ReadFromJsonAsync<RecordAnswerResponse>())!;
    }

    [Fact]
    public async Task ClosesTheMomentOneTeamLeads_AfterEveryTeamHasHadATurn()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SuddenDeathMatchAsync(h);

        var first = await ServeAndAnswerAsync(h, started, "Correct");
        var second = await ServeAndAnswerAsync(h, started, "Incorrect");

        first.SegmentComplete.Should().BeFalse("the second team has not had its turn yet");
        second.SegmentComplete.Should().BeTrue();
        second.MatchComplete.Should().BeTrue();
        second.NextQuestion.Should().BeNull();
        (await h.ReadDbAsync(db => db.MatchSegments.SingleAsync(s => s.Id == started.Segments[0].Id))).State
            .Should().Be(MatchSegmentState.Completed);
        (await h.ReadDbAsync(db => db.MatchQuestions.CountAsync(
            q => q.MatchSegmentId == started.Segments[0].Id && q.State == MatchQuestionState.Released))).Should().Be(4);
        var timeline = (await h.Client.GetFromJsonAsync<MatchTimelineResponse>($"{started.Live}/timeline"))!;
        timeline.Events.Should().Contain(e => e.EventType == "SegmentCompleted" && e.Detail!.Contains("Sudden death"));
    }

    [Fact]
    public async Task StaysOpen_WhileTheTeamsAreLevel()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SuddenDeathMatchAsync(h);

        await ServeAndAnswerAsync(h, started, "Correct");
        var levelAgain = await ServeAndAnswerAsync(h, started, "Correct");

        levelAgain.SegmentComplete.Should().BeFalse();
        levelAgain.NextQuestion.Should().NotBeNull();
    }

    [Fact]
    public async Task ASkippedQuestion_AlsoCountsAsATurn()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SuddenDeathMatchAsync(h);
        await ServeAndAnswerAsync(h, started, "Correct");
        var second = await h.ServeAsync(started);

        await h.Client.PostAsJsonAsync($"{started.Live}/questions/{second.MatchQuestionId}/skip", new SkipQuestionRequest("No answer"));

        (await h.ReadDbAsync(db => db.MatchSegments.SingleAsync(s => s.Id == started.Segments[0].Id))).State
            .Should().Be(MatchSegmentState.Completed);
    }

    [Fact]
    public async Task AnOrdinarySegment_DoesNotCloseEarly()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SuddenDeathMatchAsync(h, suddenDeath: false);

        await ServeAndAnswerAsync(h, started, "Correct");
        var second = await ServeAndAnswerAsync(h, started, "Incorrect");

        second.SegmentComplete.Should().BeFalse();
    }
}
