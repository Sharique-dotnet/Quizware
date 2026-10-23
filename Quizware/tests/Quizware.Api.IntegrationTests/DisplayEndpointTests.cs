using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quizware.Api.Contracts.V1.Display;
using Quizware.Application.Authorization;

namespace Quizware.Api.IntegrationTests;

/// <summary>P12-07: the display API — read-only screen payloads for a display
/// token, with the answer withheld until it is revealed.</summary>
public class DisplayEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DisplayEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MatchState_ShowsTheQuestionAndScores_ButNotTheAnswerUntilItIsRevealed()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var display = await h.CreateClientAsync(Roles.Display);
        var url = $"/api/v1/display/matches/{started.Match.Id}/state";

        var before = (await display.GetFromJsonAsync<DisplayMatchStateResponse>(url))!;
        (await h.Client.PostAsync($"{started.Live}/questions/{question.MatchQuestionId}/reveal", null)).EnsureSuccessStatusCode();
        var after = (await display.GetFromJsonAsync<DisplayMatchStateResponse>(url))!;

        before.State.Should().Be("InProgress");
        before.CurrentSegmentFormat.Should().Be("Mcq");
        before.CurrentQuestionText.Should().Be(question.QuestionText);
        before.CurrentQuestion!.Options.Should().HaveCount(2);
        before.CurrentQuestion.CorrectOptionId.Should().BeNull();
        before.AnswerRevealed.Should().BeFalse();
        before.ActiveParticipant.Should().NotBeNull();
        before.Participants.Should().HaveCount(2);
        after.AnswerRevealed.Should().BeTrue();
        after.CurrentQuestion!.CorrectOptionId.Should().NotBeNull();
    }

    [Fact]
    public async Task ScoresStandingsAndBranding_AreServedToTheDisplay()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateReadyToStartMatchAsync(2, 2));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!;
        await h.AnswerAsync(started, question.MatchQuestionId, holder.ParticipantId, "Correct");
        await h.SeedAsync((db, programId) => db.Programs.Single(p => p.Id == programId)
            .UpdateDetails("Branded", null, null, "https://cdn.example/logo.png", "#123456", "#abcdef", "Noto Nastaliq Urdu", "test"));
        var display = await h.CreateClientAsync(Roles.Display);

        var scores = (await display.GetFromJsonAsync<DisplayScoresResponse>($"/api/v1/display/matches/{started.Match.Id}/scores"))!;
        var standings = (await display.GetFromJsonAsync<DisplayStandingsResponse>($"/api/v1/display/programs/{h.ProgramId}/standings"))!;
        var branding = (await display.GetFromJsonAsync<DisplayBrandingResponse>($"/api/v1/display/programs/{h.ProgramId}/branding"))!;

        scores.Scores.Should().HaveCount(2);
        scores.Scores.Single(s => s.TeamId == holder.TeamId).Score.Should().Be(10);
        standings.Standings.First().TeamId.Should().Be(holder.TeamId);
        branding.Should().Be(new DisplayBrandingResponse("https://cdn.example/logo.png", "#123456", "#abcdef", "Noto Nastaliq Urdu"));
    }

    [Fact]
    public async Task TheDisplayApi_IsForDisplayTokensOnly_AndOnlyForTheirOwnProgram()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var other = await MatchTestHarness.CreateAsync(_factory);
        var display = await h.CreateClientAsync(Roles.Display);
        var match = await h.CreateReadyToStartMatchAsync(2, 2);

        var asOperator = await h.Client.GetAsync($"/api/v1/display/matches/{match.Id}/state");
        var otherProgram = await display.GetAsync($"/api/v1/display/programs/{other.ProgramId}/branding");
        var otherMatch = await display.GetAsync($"/api/v1/display/matches/{(await other.CreateReadyToStartMatchAsync(2, 2)).Id}/state");

        asOperator.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        otherProgram.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        otherMatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
