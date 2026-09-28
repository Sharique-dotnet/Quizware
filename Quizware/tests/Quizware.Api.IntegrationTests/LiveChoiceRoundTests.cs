using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;

namespace Quizware.Api.IntegrationTests;

/// <summary>P9-09: the Choice round's topic board, built from each question's
/// TopicLabel, with exclusive topics removed once picked.</summary>
public class LiveChoiceRoundTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveChoiceRoundTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<MatchTestHarness.StartedMatch> ChoiceMatchAsync(
        MatchTestHarness h, params (string Label, bool Exclusive, int? Order)[] questions)
    {
        await h.ResetScoringDefaultsAsync();
        await h.SeedAsync((db, p) =>
        {
            foreach (var (label, exclusive, order) in questions)
            {
                var q = ChoiceQuestion.Create(p, QuestionOwnerScope.Program, $"About {label} {Guid.NewGuid():N}", label,
                    DifficultyLevel.Medium, "en", "t", isTopicExclusive: exclusive, topicDisplayOrder: order);
                q.Approve(Guid.NewGuid());
                db.Questions.Add(q);
                db.QuestionOptions.Add(QuestionOption.Create(q.Id, "Right", true, 0, "t"));
                db.QuestionOptions.Add(QuestionOption.Create(q.Id, "Wrong", false, 1, "t"));
            }
        });
        var stage = await h.CreateStageAsync(("Choice", questions.Length));
        await h.ConfigureTemplatesAsync(stage.Id, topicMode: TopicSelectionMode.TeamPicksTopic);
        return await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2)));
    }

    private static async Task<IReadOnlyList<string>> BoardAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started) =>
        (await h.Client.GetFromJsonAsync<AvailableTopicsResponse>($"{started.Live}/topics/available"))!.Topics;

    private static async Task<HttpResponseMessage> PickAsync(MatchTestHarness h, MatchTestHarness.StartedMatch started, string topic) =>
        await h.Client.PostAsJsonAsync(
            $"{started.Live}/topics/select",
            new SelectTopicRequest((await h.StateAsync(started)).ActiveParticipant!.ParticipantId, topic));

    [Fact]
    public async Task TheBoard_ListsTopicLabels_InBoardOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await ChoiceMatchAsync(h, ("Sports", true, 2), ("Science", true, 1), ("Art", true, null));

        (await BoardAsync(h, started)).Should().Equal("Science", "Sports", "Art");
    }

    [Fact]
    public async Task PickingAnExclusiveTopic_ServesIt_AndClearsItsOtherQuestionsFromTheBoard()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await ChoiceMatchAsync(h, ("Sports", true, 1), ("Sports", true, 1), ("Science", true, 2));

        var pick = await PickAsync(h, started, "sports");
        var served = await h.ServeAsync(started);
        var board = await BoardAsync(h, started);

        pick.StatusCode.Should().Be(HttpStatusCode.OK);
        served.TopicName.Should().Be("Sports");
        board.Should().Equal("Science");
        (await h.ReadDbAsync(db => db.MatchQuestions.CountAsync(
            q => q.MatchSegmentId == started.Segments[0].Id && q.State == MatchQuestionState.Released))).Should().Be(1);
    }

    [Fact]
    public async Task ANonExclusiveTopic_StaysOnTheBoard_WhileItHasQuestionsLeft()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await ChoiceMatchAsync(h, ("History", false, 1), ("History", false, 1), ("Science", true, 2));

        await PickAsync(h, started, "History");
        await h.ServeAsync(started);

        (await BoardAsync(h, started)).Should().Equal("History", "Science");
    }
}
