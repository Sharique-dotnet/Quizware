using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quizware.Api.Contracts.V1.LiveMatch;
using Quizware.Domain.Enums;
using Quizware.Domain.QuestionBank;
using Quizware.Infrastructure.Persistence;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 9f: how each of the ten formats is presented live and how
/// its responses are checked.</summary>
public class LiveQuestionFormatsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LiveQuestionFormatsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static T Approved<T>(T question)
        where T : Question
    {
        question.Approve(Guid.NewGuid());
        return question;
    }

    private static void AddOptions(AppDbContext db, Guid questionId, int correctIndex = 0, int count = 3)
    {
        for (var i = 0; i < count; i++)
        {
            db.QuestionOptions.Add(QuestionOption.Create(questionId, $"Option {i}", i == correctIndex, i, "test"));
        }
    }

    /// <summary>A started 2-team match whose only segment is
    /// <paramref name="format"/>, with its first segment open.</summary>
    private static async Task<MatchTestHarness.StartedMatch> SingleFormatMatchAsync(
        MatchTestHarness h, string format, Action<AppDbContext, Guid> seed, int questions = 1)
    {
        await h.ResetScoringDefaultsAsync();
        await h.SeedAsync(seed);
        var stage = await h.CreateStageAsync((format, questions));
        var match = await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2));
        return await h.StartWithFirstSegmentOpenAsync(match);
    }

    [Fact]
    public async Task EveryOneOfTheTenFormats_CanBeReservedServedAndPresented()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedAsync((db, p) =>
        {
            var mcq = Approved(McqQuestion.Create(p, QuestionOwnerScope.Program, "Mcq?", DifficultyLevel.Medium, "en", "t"));
            var buzzer = Approved(BuzzerQuestion.Create(p, QuestionOwnerScope.Program, "Buzz?", DifficultyLevel.Medium, "en", "t"));
            var card = Approved(CardQuestion.Create(p, QuestionOwnerScope.Program, "Card?", DifficultyLevel.Medium, "en", "t"));
            var choice = Approved(ChoiceQuestion.Create(p, QuestionOwnerScope.Program, "Choice?", "Sport", DifficultyLevel.Medium, "en", "t"));
            var passing = Approved(PassingQuestion.Create(p, QuestionOwnerScope.Program, "Pass?", DifficultyLevel.Medium, "en", "t"));
            var tieBreaker = Approved(TieBreakerQuestion.CreateNumericProximity(p, QuestionOwnerScope.Program, "How many?", 42m, DifficultyLevel.Medium, "en", "t"));
            var audioVisual = Approved(AudioVisualQuestion.Create(
                p, QuestionOwnerScope.Program, "Name the tune", Guid.NewGuid(), MediaKind.Audio, "Anthem", DifficultyLevel.Medium, "en", "t"));
            var rapidFire = Approved(RapidFireQuestion.CreateStored(p, QuestionOwnerScope.Program, "Capital of France?", "Paris", DifficultyLevel.Medium, "en", "t"));
            var sequence = Approved(SequenceQuestion.Create(p, QuestionOwnerScope.Program, "Order these", 2, DifficultyLevel.Medium, "en", "t"));
            var visual = Approved(VisualRapidFireQuestion.Create(p, QuestionOwnerScope.Program, 2, DifficultyLevel.Medium, "en", "t"));
            db.Questions.AddRange(mcq, buzzer, card, choice, passing, tieBreaker, audioVisual, rapidFire, sequence, visual);
            foreach (var withOptions in new Question[] { mcq, buzzer, card, choice, passing, tieBreaker })
            {
                AddOptions(db, withOptions.Id);
            }

            db.SequenceItems.Add(SequenceItem.Create(sequence.Id, 2, 0, "t", "Second"));
            db.SequenceItems.Add(SequenceItem.Create(sequence.Id, 1, 1, "t", "First"));
            db.VisualRapidFireItems.Add(VisualRapidFireItem.Create(visual.Id, Guid.NewGuid(), "Eiffel Tower", 0, "t"));
            db.VisualRapidFireItems.Add(VisualRapidFireItem.Create(visual.Id, Guid.NewGuid(), "Big Ben", 1, "t"));
        });
        string[] formats = ["Mcq", "Buzzer", "Card", "Choice", "Passing", "TieBreaker", "AudioVisual", "RapidFire", "Sequence", "VisualRapidFire"];
        var stage = await h.CreateStageAsync(formats.Select(f => (f, 1)).ToArray());
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2)));
        var served = new Dictionary<string, CurrentQuestionDto>();

        for (var i = 0; i < formats.Length; i++)
        {
            if (i > 0)
            {
                (await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[i].Id}/open", null)).EnsureSuccessStatusCode();
            }

            var question = await h.ServeAsync(started, i);
            served[question.Format] = question;
            (await h.Client.PostAsJsonAsync($"{started.Live}/questions/{question.MatchQuestionId}/skip", new SkipQuestionRequest("next")))
                .EnsureSuccessStatusCode();
            (await h.Client.PostAsync($"{started.Live}/segments/{started.Segments[i].Id}/close", null)).EnsureSuccessStatusCode();
        }

        served.Keys.Should().BeEquivalentTo(formats);
        foreach (var optionFormat in new[] { "Mcq", "Buzzer", "Card", "Choice", "Passing", "TieBreaker" })
        {
            served[optionFormat].Options.Should().HaveCount(3, optionFormat);
            served[optionFormat].CorrectOptionId.Should().NotBeNull(optionFormat);
        }

        served["AudioVisual"].MediaUrl.Should().StartWith("/api/v1/media/");
        served["AudioVisual"].Options.Should().BeNull();
        served["RapidFire"].Options.Should().BeNull();
        served["Sequence"].Options!.Select(o => o.Text).Should().Equal("Second", "First");
        served["VisualRapidFire"].Options.Should().HaveCount(2).And.OnlyContain(o => o.Text.StartsWith("/api/v1/media/"));
    }

    [Fact]
    public async Task Mcq_WithShuffleOff_KeepsTheAuthoredOptionOrder()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SingleFormatMatchAsync(h, "Mcq", (db, p) =>
        {
            var q = Approved(McqQuestion.Create(p, QuestionOwnerScope.Program, "Q", DifficultyLevel.Medium, "en", "t", shuffleOptions: false));
            db.Questions.Add(q);
            AddOptions(db, q.Id, count: 4);
        });

        var question = await h.ServeAsync(started);

        question.Options!.Select(o => o.Text).Should().Equal("Option 0", "Option 1", "Option 2", "Option 3");
    }

    [Fact]
    public async Task Mcq_WithSeveralCorrectOptions_NamesNone_ButChecksTheWholeSelection()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SingleFormatMatchAsync(h, "Mcq", (db, p) =>
        {
            var q = Approved(McqQuestion.Create(p, QuestionOwnerScope.Program, "Q", DifficultyLevel.Medium, "en", "t", allowMultipleCorrect: true));
            db.Questions.Add(q);
            db.QuestionOptions.Add(QuestionOption.Create(q.Id, "Right 1", true, 0, "t"));
            db.QuestionOptions.Add(QuestionOption.Create(q.Id, "Right 2", true, 1, "t"));
            db.QuestionOptions.Add(QuestionOption.Create(q.Id, "Wrong", false, 2, "t"));
        });
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        var rights = question.Options!.Where(o => o.Text.StartsWith("Right")).Select(o => o.OptionId).ToList();

        var partial = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", rights.Take(1).ToList());
        var full = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", rights);

        question.CorrectOptionId.Should().BeNull();
        partial.StatusCode.Should().Be(HttpStatusCode.BadRequest, "only one of the two correct options was chosen");
        full.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AudioVisual_SwitchesToTheRevealMedia_AndMarksAnAcceptedTypedAnswerCorrect()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var clip = Guid.NewGuid();
        var revealClip = Guid.NewGuid();
        var started = await SingleFormatMatchAsync(h, "AudioVisual", (db, p) => db.Questions.Add(Approved(AudioVisualQuestion.Create(
            p, QuestionOwnerScope.Program, "Name the tune", clip, MediaKind.Audio, "Ode to Joy", DifficultyLevel.Medium, "en", "t",
            acceptableAnswersJson: "[\"Beethoven 9\"]", revealMediaAssetId: revealClip))));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;

        var revealed = (await (await h.Client.PostAsync($"{started.Live}/questions/{question.MatchQuestionId}/reveal", null))
            .Content.ReadFromJsonAsync<CurrentQuestionDto>())!;
        var answer = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", null, "  beethoven   9 ");

        question.MediaUrl.Should().Be($"/api/v1/media/{clip}");
        revealed.MediaUrl.Should().Be($"/api/v1/media/{revealClip}");
        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await answer.Content.ReadFromJsonAsync<RecordAnswerResponse>())!.AnswerRecordId;
        (await h.ReadDbAsync(db => db.AnswerRecords.SingleAsync(a => a.Id == id))).IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task AudioVisual_ATypedAnswerTheOperatorAcceptsAnyway_IsNotOverruled()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SingleFormatMatchAsync(h, "AudioVisual", (db, p) => db.Questions.Add(Approved(AudioVisualQuestion.Create(
            p, QuestionOwnerScope.Program, "Name the tune", Guid.NewGuid(), MediaKind.Audio, "Ode to Joy", DifficultyLevel.Medium, "en", "t"))));
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;

        var answer = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", null, "Oda to Joy");

        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await answer.Content.ReadFromJsonAsync<RecordAnswerResponse>())!.AnswerRecordId;
        (await h.ReadDbAsync(db => db.AnswerRecords.SingleAsync(a => a.Id == id))).IsCorrect.Should().BeNull();
    }

    [Fact]
    public async Task Sequence_TheSubmittedOrderIsChecked()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SingleFormatMatchAsync(h, "Sequence", (db, p) =>
        {
            var q = Approved(SequenceQuestion.Create(p, QuestionOwnerScope.Program, "Oldest first", 3, DifficultyLevel.Medium, "en", "t"));
            db.Questions.Add(q);
            db.SequenceItems.Add(SequenceItem.Create(q.Id, 3, 0, "t", "C"));
            db.SequenceItems.Add(SequenceItem.Create(q.Id, 1, 1, "t", "A"));
            db.SequenceItems.Add(SequenceItem.Create(q.Id, 2, 2, "t", "B"));
        });
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;
        var byText = question.Options!.ToDictionary(o => o.Text, o => o.OptionId);

        var wrongOrder = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", [byText["C"], byText["A"], byText["B"]]);
        var incomplete = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", [byText["A"], byText["B"]]);
        var right = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", [byText["A"], byText["B"], byText["C"]]);

        question.Options!.Select(o => o.Text).Should().Equal("C", "A", "B");
        wrongOrder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        incomplete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        right.StatusCode.Should().Be(HttpStatusCode.OK);
        (await right.Content.ReadFromJsonAsync<RecordAnswerResponse>())!.PointsAwarded.Should().Be(20);
    }

    [Fact]
    public async Task TieBreaker_NumericAnswer_IsCheckedExactly()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SingleFormatMatchAsync(h, "TieBreaker", (db, p) => db.Questions.Add(Approved(
            TieBreakerQuestion.CreateNumericProximity(p, QuestionOwnerScope.Program, "Metres in a mile?", 1609.34m, DifficultyLevel.Medium, "en", "t"))));
        await h.AddScoringRuleAsync("TieBreaker", "Correct", null, 10);
        var question = await h.ServeAsync(started);
        var holder = (await h.StateAsync(started)).ActiveParticipant!.ParticipantId;

        var answer = await PostAnswerAsync(h, started, question.MatchQuestionId, holder, "Correct", null, "1609.34");

        answer.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await answer.Content.ReadFromJsonAsync<RecordAnswerResponse>())!.AnswerRecordId;
        (await h.ReadDbAsync(db => db.AnswerRecords.SingleAsync(a => a.Id == id))).IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task Buzzer_UsesItsBuzzWindow_WhenNoOtherTimerIsSet()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        var started = await SingleFormatMatchAsync(h, "Buzzer", (db, p) => db.Questions.Add(Approved(
            BuzzerQuestion.Create(p, QuestionOwnerScope.Program, "Buzz", DifficultyLevel.Medium, "en", "t", buzzWindowSeconds: 12))));

        var question = await h.ServeAsync(started);

        question.TimeLimitSeconds.Should().Be(12);
    }

    [Fact]
    public async Task ASegmentTimer_OverridesTheFormatDefault()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);
        await h.ResetScoringDefaultsAsync();
        await h.SeedAsync((db, p) => db.Questions.Add(Approved(
            BuzzerQuestion.Create(p, QuestionOwnerScope.Program, "Buzz", DifficultyLevel.Medium, "en", "t", buzzWindowSeconds: 12))));
        var stage = await h.CreateStageAsync(("Buzzer", 1));
        await h.ConfigureTemplatesAsync(stage.Id, timeLimitSeconds: 45);
        var started = await h.StartWithFirstSegmentOpenAsync(await h.CreateMatchAsync(stage.Id, 1, await h.CreateTeamsAsync(2)));

        var question = await h.ServeAsync(started);

        question.TimeLimitSeconds.Should().Be(45);
    }

    private static Task<HttpResponseMessage> PostAnswerAsync(
        MatchTestHarness h, MatchTestHarness.StartedMatch started, Guid matchQuestionId, Guid participantId, string outcome,
        IReadOnlyList<Guid>? selectedIds, string? freeText = null) =>
        h.Client.PostAsJsonAsync($"{started.Live}/answers", new RecordAnswerRequest(
            matchQuestionId, participantId, outcome, null, selectedIds, freeText, 0, "Operator", null, null));

    [Fact]
    public async Task EveryFormat_HasExactlyOneLiveHandler()
    {
        var h = await MatchTestHarness.CreateAsync(_factory);

        var handled = await h.UseServiceAsync<IEnumerable<Application.Gameplay.Formats.IQuestionFormatHandler>, List<QuestionFormatCode>>(
            handlers => Task.FromResult(handlers.Select(x => x.Format).ToList()));

        handled.Should().BeEquivalentTo(Enum.GetValues<QuestionFormatCode>());
    }
}
