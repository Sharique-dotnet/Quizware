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

        types.Should().Equal("MatchStateChanged", "AnswerRecorded", "ParticipantRemoved", "MatchCompleted");
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
        (await OutboxTypesAsync(h, started.Match.Id)).Should().Equal("MatchStateChanged");
    }
}
