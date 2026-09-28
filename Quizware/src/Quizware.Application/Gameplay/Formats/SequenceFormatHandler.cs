using Microsoft.EntityFrameworkCore;
using Quizware.Application.Abstractions;
using Quizware.Application.Gameplay.Dtos;
using Quizware.Domain.Enums;
using Quizware.Domain.Gameplay;
using Quizware.Domain.QuestionBank;
using ValidationException = Quizware.Application.Common.Exceptions.ValidationException;

namespace Quizware.Application.Gameplay.Formats;

/// <summary>The team puts the items in order. Items are listed in their
/// authored display order (an item with only media shows its URL); the answer
/// is the item ids in the team's order, checked against each item's correct
/// position.</summary>
public sealed class SequenceFormatHandler : IQuestionFormatHandler
{
    private readonly IAppDbContext _db;

    public SequenceFormatHandler(IAppDbContext db)
    {
        _db = db;
    }

    public QuestionFormatCode Format => QuestionFormatCode.Sequence;

    public bool AnyTeamMayAnswer => false;

    public int? DefaultTimeLimitSeconds(Question question) => null;

    public bool WrongAnswerLeavesQuestionOpen(Question question) => false;

    public QuestionPassRules? PassRules(Question question) => null;

    public TopicChoice? TopicChoice(Question question) => null;

    public async Task<FormatContent> PresentAsync(Question question, MatchQuestion matchQuestion, CancellationToken cancellationToken)
    {
        var items = await _db.SequenceItems.Where(i => i.QuestionId == question.Id).OrderBy(i => i.DisplayOrder).ToListAsync(cancellationToken);
        var options = items
            .Select((item, i) => new CurrentQuestionOptionDto(
                item.Id, item.ItemText ?? (item.MediaAssetId is { } media ? QuestionFormatHandlers.MediaUrl(media) : string.Empty), i))
            .ToList();
        return new FormatContent(options, null, null);
    }

    public async Task<ResponseEvaluation> EvaluateAsync(
        Question question, IReadOnlyList<Guid>? selectedIds, string? freeText, CancellationToken cancellationToken)
    {
        if (selectedIds is null)
        {
            return ResponseEvaluation.NotChecked;
        }

        var items = await _db.SequenceItems.Where(i => i.QuestionId == question.Id).ToListAsync(cancellationToken);
        if (selectedIds.Count != items.Count
            || selectedIds.Distinct().Count() != items.Count
            || selectedIds.Any(id => items.All(i => i.Id != id)))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["selectedOptionIds"] = ["A sequence answer must list every item of the question exactly once, in the team's order."],
            });
        }

        var correctOrder = items.OrderBy(i => i.CorrectPosition).Select(i => i.Id);
        return new ResponseEvaluation(correctOrder.SequenceEqual(selectedIds), true);
    }
}
