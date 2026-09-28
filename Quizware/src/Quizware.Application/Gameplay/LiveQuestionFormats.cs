using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Gameplay;

/// <summary>What differs between the ten formats on the night: what the
/// screen shows, the default timer, and how a team's response is checked.
/// Everything else in the engine is format-agnostic.
///
/// The live contract has one media URL and a flat option list, so: option
/// formats list their options; Sequence lists its items (in authored display
/// order — the team puts them in order); VisualRapidFire lists one entry per
/// image whose Text is that image's URL; AudioVisual uses MediaUrl (switching
/// to the reveal media, if any, once revealed).</summary>
public sealed class LiveQuestionFormats
{
    private readonly IAppDbContext _db;

    public LiveQuestionFormats(IAppDbContext db)
    {
        _db = db;
    }

    public static string MediaUrl(Guid mediaAssetId) => $"/api/v1/media/{mediaAssetId}";

    public sealed record Content(IReadOnlyList<CurrentQuestionOptionDto> Options, Guid? CorrectOptionId, string? MediaUrl);

    /// <summary>A format's own timer, used when neither the question nor the
    /// segment sets one.</summary>
    public static int? DefaultTimeLimitSeconds(Question question) => question switch
    {
        BuzzerQuestion buzzer => buzzer.BuzzWindowSeconds,
        RapidFireQuestion { BurstSeconds: { } burst } => burst,
        _ => null,
    };

    public async Task<Content> PresentAsync(Question question, MatchQuestion matchQuestion, CancellationToken cancellationToken)
    {
        switch (question)
        {
            case SequenceQuestion:
            {
                var items = await _db.SequenceItems.Where(i => i.QuestionId == question.Id).OrderBy(i => i.DisplayOrder).ToListAsync(cancellationToken);
                var options = items
                    .Select((item, i) => new CurrentQuestionOptionDto(
                        item.Id, item.ItemText ?? (item.MediaAssetId is { } media ? MediaUrl(media) : string.Empty), i))
                    .ToList();
                return new Content(options, null, null);
            }

            case VisualRapidFireQuestion:
            {
                var items = await _db.VisualRapidFireItems.Where(i => i.QuestionId == question.Id).OrderBy(i => i.DisplayOrder).ToListAsync(cancellationToken);
                var options = items.Select((item, i) => new CurrentQuestionOptionDto(item.Id, MediaUrl(item.MediaAssetId), i)).ToList();
                return new Content(options, null, null);
            }

            case AudioVisualQuestion audioVisual:
            {
                var media = matchQuestion.RevealedAtUtc is not null && audioVisual.RevealMediaAssetId is { } reveal
                    ? reveal
                    : audioVisual.MediaAssetId;
                return new Content([], null, MediaUrl(media));
            }
        }

        var questionOptions = await _db.QuestionOptions.Where(o => o.QuestionId == question.Id).ToListAsync(cancellationToken);
        var shuffle = question is not McqQuestion { ShuffleOptions: false };
        var ordered = OrderOptions(questionOptions, shuffle ? matchQuestion.OptionOrderJson : null)
            .Select((o, i) => new CurrentQuestionOptionDto(o.Id, o.OptionText, i))
            .ToList();

        // The contract carries a single correct option; with several correct
        // options no one of them is "the" answer, so none is named.
        var correct = questionOptions.Where(o => o.IsCorrect).ToList();
        return new Content(ordered, correct.Count == 1 ? correct[0].Id : null, null);
    }

    public sealed record Evaluation(bool? IsCorrect, bool IsObjective);

    /// <summary>Checks a response where the format allows it. Option and
    /// sequence answers are objective — the recorded outcome must agree with
    /// them. A typed answer that matches an accepted answer is marked correct,
    /// but free text is never used to overrule the operator's judgement
    /// (spelling, accents, "close enough" calls stay with the operator).</summary>
    public async Task<Evaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken)
    {
        if (question is SequenceQuestion)
        {
            return selectedIds is null ? new Evaluation(null, false) : new Evaluation(await EvaluateSequenceAsync(question, selectedIds, cancellationToken), true);
        }

        if (selectedIds is not null)
        {
            var options = await _db.QuestionOptions.Where(o => o.QuestionId == question.Id).ToListAsync(cancellationToken);
            if (options.Count == 0 || selectedIds.Any(id => options.All(o => o.Id != id)))
            {
                throw Invalid("selectedOptionId", "A selected option does not belong to this question.");
            }

            var correct = options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
            return new Evaluation(correct.SetEquals(selectedIds), true);
        }

        return freeText is null ? new Evaluation(null, false) : new Evaluation(MatchesAcceptedAnswer(question, freeText) ? true : null, false);
    }

    private async Task<bool> EvaluateSequenceAsync(Question question, IReadOnlyList<Guid> submittedOrder, CancellationToken cancellationToken)
    {
        var items = await _db.SequenceItems.Where(i => i.QuestionId == question.Id).ToListAsync(cancellationToken);
        if (submittedOrder.Count != items.Count
            || submittedOrder.Distinct().Count() != items.Count
            || submittedOrder.Any(id => items.All(i => i.Id != id)))
        {
            throw Invalid("selectedOptionIds", "A sequence answer must list every item of the question exactly once, in the team's order.");
        }

        var correctOrder = items.OrderBy(i => i.CorrectPosition).Select(i => i.Id);
        return correctOrder.SequenceEqual(submittedOrder);
    }

    private static bool MatchesAcceptedAnswer(Question question, string freeText)
    {
        if (question is TieBreakerQuestion { AnswerMode: TieBreakAnswerMode.NumericProximity, NumericAnswer: { } target })
        {
            return decimal.TryParse(freeText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number == target;
        }

        var (answer, acceptableJson) = question switch
        {
            AudioVisualQuestion q => (q.AnswerText, q.AcceptableAnswersJson),
            RapidFireQuestion q => (q.AnswerText, q.AcceptableAnswersJson),
            TieBreakerQuestion { AnswerMode: TieBreakAnswerMode.ExactText } q => (q.AnswerText, null),
            _ => ((string?)null, (string?)null),
        };

        var accepted = new List<string>();
        if (answer is not null)
        {
            accepted.Add(answer);
        }

        if (!string.IsNullOrWhiteSpace(acceptableJson))
        {
            accepted.AddRange(JsonSerializer.Deserialize<List<string>>(acceptableJson) ?? []);
        }

        var given = Normalize(freeText);
        return accepted.Any(a => Normalize(a) == given);
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>OptionOrderJson is the per-match shuffle fixed at reservation
    /// time; without one, options appear in their authored order.</summary>
    private static IEnumerable<QuestionOption> OrderOptions(IReadOnlyList<QuestionOption> options, string? optionOrderJson)
    {
        var order = string.IsNullOrWhiteSpace(optionOrderJson) ? null : JsonSerializer.Deserialize<List<Guid>>(optionOrderJson);
        if (order is null || order.Count == 0)
        {
            return options.OrderBy(o => o.DisplayOrder);
        }

        return options.OrderBy(o =>
        {
            var index = order.IndexOf(o.Id);
            return index < 0 ? int.MaxValue : index;
        });
    }

    private static ValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
