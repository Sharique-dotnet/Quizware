using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;

namespace Quizware.Api.IntegrationTests;

/// <summary>P9-06: the match start, answer, disqualify and end transactions
/// each write their live notification to the outbox — in the same
/// transaction, so a refused action writes nothing.</summary>
public class MatchOutboxTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MatchOutboxTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static Task<List<string>> OutboxTypesAsync(MatchTestHarness h, Guid matchId) =>
        h.ReadDbAsync(db => db.OutboxMessages
            .Where(m => m.PayloadJson.Contains(matchId.ToString()))
            .OrderBy(m => m.OccurredAtUtc)
            .Select(m => m.Type)
            .ToListAsync());

    [Fact]
    public async Task StartAnswerDisqualifyAndEnd_EachWriteTheirNotification()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(3, 3));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        await h.AnswerAsync(started, question.MatchQuestionId, holder, "Correct");
        await h.DisqualifyAsync(started, holder);
        await h.Client.PostAsync($"{started.Live}/end", null);

        var types = await OutboxTypesAsync(h, started.Match.Id);

        types.Should().Equal("MatchStateChanged", "SegmentChanged", "QuestionServed", "AnswerRecorded", "ParticipantRemoved", "MatchCompleted");
        var answer = await h.ReadDbAsync(db => db.OutboxMessages.SingleAsync(
            m => m.Type == "AnswerRecorded" && m.PayloadJson.Contains(started.Match.Id.ToString())));
        answer.PayloadJson.Should().Contain(holder.ToString()).And.Contain("\"points\":10");
        answer.ProcessedAtUtc.Should().BeNull("delivery is the dispatcher's job");
    }

    [Fact]
    public async Task ARefusedAnswer_WritesNoNotification()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);

        var refused = await h.AnswerAsync(started, question.MatchQuestionId, (await h.StateAsync(started)).ActiveParticipant!.ParticipantId, "NoAnswer");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await OutboxTypesAsync(h, started.Match.Id)).Should().Equal("MatchStateChanged", "SegmentChanged", "QuestionServed");
    }

    [Fact]
    public async Task EveryOtherLiveAction_WritesItsLiveEvent_WithTheTimelineEntryAsItsCause()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 3));
        var first = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!;
        var answer = await (await h.AnswerAsync(started, first.MatchQuestionId, holder.ParticipantId, "Correct"))
            .Content.ReadFromJsonAsync<RecordAnswerResponse>();
        (await h.Client.PostAsJsonAsync($"{started.Live}/answers/{answer!.AnswerRecordId}/reverse", new ReverseAnswerRequest("Wrong team"))).EnsureSuccessStatusCode();
        (await h.Client.PostAsync($"{started.Live}/questions/{first.MatchQuestionId}/reveal", null)).EnsureSuccessStatusCode();
        (await h.Client.PostAsJsonAsync($"{started.Live}/questions/{first.MatchQuestionId}/skip", new SkipQuestionRequest("Disputed"))).EnsureSuccessStatusCode();
        (await h.Client.PostAsync($"{started.Live}/pause", null)).EnsureSuccessStatusCode();
        (await h.Client.PostAsync($"{started.Live}/resume", null)).EnsureSuccessStatusCode();
        (await h.Client.PostAsJsonAsync(
            $"/api/v1/matches/{started.Match.Id}/scores/adjust", new Contracts.V1.Scores.AdjustScoreRequest(holder.TeamId, 5, "Review"))).EnsureSuccessStatusCode();

        var types = await OutboxTypesAsync(h, started.Match.Id);

        types.Should().ContainInOrder(
            "QuestionServed", "AnswerRecorded", "AnswerReversed", "QuestionRevealed", "QuestionSkipped", "MatchStateChanged", "MatchStateChanged", "ScoresUpdated");
        var pause = await h.ReadDbAsync(db => db.OutboxMessages
            .Where(m => m.Type == "MatchStateChanged" && m.PayloadJson.Contains(started.Match.Id.ToString()) && m.PayloadJson.Contains("MatchPaused"))
            .SingleAsync());
        pause.PayloadJson.Should().Contain("\"cause\":\"MatchPaused\"");
    }
}
