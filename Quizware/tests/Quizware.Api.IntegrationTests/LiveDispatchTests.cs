using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Api.Hubs;
using Quizware.Api.Live;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>P12-03/04: outbox messages reach both hubs — the console with the
/// answer, displays without it until reveal — and a failed push is retried
/// without losing or reordering anything.</summary>
public class LiveDispatchTests : IClassFixture<LiveWebApplicationFactory>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly LiveWebApplicationFactory _factory;

    public LiveDispatchTests(LiveWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Collects every message of the given events a connection receives.</summary>
    private sealed class Inbox
    {
        private readonly ConcurrentQueue<LiveEventMessage> _messages = new();

        public Inbox(HubConnection connection, params string[] events)
        {
            foreach (var name in events)
            {
                connection.On<LiveEventMessage>(name, m => _messages.Enqueue(m));
            }
        }

        public IReadOnlyList<LiveEventMessage> All => _messages.ToList();

        public async Task<LiveEventMessage> NextAsync(string type, Func<LiveEventMessage, bool>? match = null)
        {
            var deadline = DateTime.UtcNow + Wait;
            while (DateTime.UtcNow < deadline)
            {
                var found = _messages.FirstOrDefault(m => m.Type == type && (match?.Invoke(m) ?? true));
                if (found is not null)
                {
                    return found;
                }

                await Task.Delay(25);
            }

            throw new TimeoutException($"No {type} arrived within {Wait.TotalSeconds}s; got: {string.Join(", ", All.Select(m => m.Type))}.");
        }
    }

    private static string Cause(LiveEventMessage message) => message.Data.GetProperty("cause").GetString()!;

    [Fact]
    public async Task AServedQuestion_ReachesBothHubs_WithTheAnswerOnlyOnTheConsole_UntilItIsRevealed()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await using var console = await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, h.Client);
        await using var display = await LiveHubConnections.ConnectAsync(_factory, DisplayHub.Path, await h.CreateClientAsync(Roles.Display));
        var consoleInbox = new Inbox(console, "QuestionServed", "QuestionRevealed");
        var displayInbox = new Inbox(display, "QuestionServed", "QuestionRevealed");
        await console.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        await display.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);

        var question = await h.ServeAsync(started);
        var consoleServed = await consoleInbox.NextAsync("QuestionServed");
        var displayServed = await displayInbox.NextAsync("QuestionServed");
        (await h.Client.PostAsync($"{started.Live}/questions/{question.MatchQuestionId}/reveal", null)).EnsureSuccessStatusCode();
        var displayRevealed = await displayInbox.NextAsync("QuestionRevealed");

        consoleServed.State!.CurrentQuestion!.CorrectOptionId.Should().NotBeNull();
        displayServed.State!.CurrentQuestion!.MatchQuestionId.Should().Be(question.MatchQuestionId);
        displayServed.State.CurrentQuestion.CorrectOptionId.Should().BeNull("a display must not see the answer before the reveal");
        displayServed.Data.GetRawText().Should().NotContainEquivalentOf("correctOptionId");
        displayRevealed.State!.CurrentQuestion!.CorrectOptionId.Should().Be(consoleServed.State.CurrentQuestion.CorrectOptionId);
        var pending = await h.ReadDbAsync(db => db.OutboxMessages.CountAsync(m => m.ProcessedAtUtc == null && m.PayloadJson.Contains(started.Match.Id.ToString())));
        pending.Should().Be(0);
    }

    [Fact]
    public async Task AFailedPush_IsRetried_AndDeliveredExactlyOnce()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await using var console = await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, h.Client);
        var inbox = new Inbox(console, "MatchStateChanged");
        await console.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        await Task.Delay(300);

        _factory.FailNextPushes(2);
        (await h.Client.PostAsync($"{started.Live}/pause", null)).EnsureSuccessStatusCode();
        var paused = await inbox.NextAsync("MatchStateChanged", m => Cause(m) == "MatchPaused");
        await Task.Delay(500);

        paused.State!.IsPaused.Should().BeTrue();
        inbox.All.Count(m => Cause(m) == "MatchPaused").Should().Be(1);
        var row = await h.ReadDbAsync(db => db.OutboxMessages.SingleAsync(
            m => m.Type == "MatchStateChanged" && m.PayloadJson.Contains(started.Match.Id.ToString()) && m.PayloadJson.Contains("MatchPaused")));
        row.RetryCount.Should().Be(2);
        row.ProcessedAtUtc.Should().NotBeNull();
        row.Error.Should().Contain("Simulated SignalR failure");
    }

    [Fact]
    public async Task WhileAPushIsBeingRetried_TheMatchsLaterEventsWaitBehindIt_InOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await using var console = await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, h.Client);
        var inbox = new Inbox(console, "MatchStateChanged");
        await console.InvokeAsync<LiveMatchStateResponse>("JoinMatch", started.Match.Id);
        await Task.Delay(300);

        _factory.FailNextPushes(1);
        (await h.Client.PostAsync($"{started.Live}/pause", null)).EnsureSuccessStatusCode();
        (await h.Client.PostAsync($"{started.Live}/resume", null)).EnsureSuccessStatusCode();
        await inbox.NextAsync("MatchStateChanged", m => Cause(m) == "MatchResumed");

        inbox.All.Select(Cause).Where(c => c is "MatchPaused" or "MatchResumed").Should().Equal("MatchPaused", "MatchResumed");
    }

    [Fact]
    public async Task APushThatKeepsFailing_IsLeftUndelivered_AndDoesNotBlockOtherMatches()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var stuck = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var other = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        await using var console = await LiveHubConnections.ConnectAsync(_factory, MatchHub.Path, h.Client);
        var inbox = new Inbox(console, "MatchStateChanged");
        await console.InvokeAsync<LiveMatchStateResponse>("JoinMatch", other.Match.Id);
        await Task.Delay(300);

        _factory.FailNextPushes(3);
        (await h.Client.PostAsync($"{stuck.Live}/pause", null)).EnsureSuccessStatusCode();
        var deadline = DateTime.UtcNow + Wait;
        int retries;
        do
        {
            await Task.Delay(100);
            retries = await h.ReadDbAsync(db => db.OutboxMessages
                .Where(m => m.PayloadJson.Contains(stuck.Match.Id.ToString()) && m.PayloadJson.Contains("MatchPaused"))
                .Select(m => m.RetryCount)
                .SingleAsync());
        }
        while (retries < 3 && DateTime.UtcNow < deadline);
        (await h.Client.PostAsync($"{other.Live}/pause", null)).EnsureSuccessStatusCode();

        await inbox.NextAsync("MatchStateChanged", m => m.MatchId == other.Match.Id && Cause(m) == "MatchPaused");
        var row = await h.ReadDbAsync(db => db.OutboxMessages.SingleAsync(
            m => m.PayloadJson.Contains(stuck.Match.Id.ToString()) && m.PayloadJson.Contains("MatchPaused")));
        row.RetryCount.Should().Be(3);
        row.ProcessedAtUtc.Should().BeNull("after the maximum retries it is left for inspection, not delivered");
    }

    [Fact]
    public void TheDisplaySanitizer_StripsAnswersAtAnyDepth_ExceptOnAReveal()
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            matchQuestionId = Guid.NewGuid(),
            correctOptionId = Guid.NewGuid(),
            detail = new { options = new[] { new { text = "A", answerText = "secret" } }, acceptedAnswers = new[] { "x" } },
        });

        var stripped = LiveEventSanitizer.ForDisplay("QuestionServed", data).GetRawText();
        var revealed = LiveEventSanitizer.ForDisplay("QuestionRevealed", data).GetRawText();

        stripped.Should().Contain("matchQuestionId").And.NotContain("correctOptionId").And.NotContain("answerText").And.NotContain("acceptedAnswers");
        revealed.Should().Contain("correctOptionId");
    }
}
